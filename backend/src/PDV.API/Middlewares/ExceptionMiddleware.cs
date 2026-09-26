using System.Net;
using System.Text.Json;
using PDV.Domain.Exceptions;

namespace PDV.API.Middlewares;

/// <summary>
/// Captura exceções não tratadas e retorna respostas padronizadas.
/// Separa DomainException (400) de erros internos (500).
/// </summary>
public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Domain rule violation: {Message}", ex.Message);
            await WriteResponse(context, HttpStatusCode.BadRequest, ex.Message, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteResponse(context, HttpStatusCode.InternalServerError, "Erro interno do servidor.", logger);
        }
    }

    private static Task WriteResponse(HttpContext context, HttpStatusCode status, string message, ILogger logger)
    {
        // Se os headers já foram enviados, mexer no StatusCode lança InvalidOperationException
        // e mata a conexão — o erro original vira um "erro no handler de erro". Nesse ponto
        // não há resposta a corrigir: o que dá para fazer é registrar e abortar a conexão,
        // para que o cliente veja um corpo truncado em vez de um JSON aparentemente íntegro.
        if (context.Response.HasStarted)
        {
            logger.LogWarning(
                "Resposta já iniciada para {Method} {Path}; não é possível devolver {Status}.",
                context.Request.Method, context.Request.Path, (int)status);
            context.Abort();
            return Task.CompletedTask;
        }

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new { error = message });
        return context.Response.WriteAsync(body);
    }
}

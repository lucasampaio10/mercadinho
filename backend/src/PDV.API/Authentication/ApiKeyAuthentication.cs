using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PDV.API.Authentication;

/// <summary>
/// Autenticação por chave compartilhada, enviada no header <c>X-API-Key</c>.
///
/// O PDV roda em rede local e é acessado pelo celular do balcão — não há usuários,
/// tampouco um provedor de identidade. Mas "rede local" não é fronteira de segurança:
/// sem nenhuma barreira, qualquer aparelho no Wi-Fi da loja (inclusive o do cliente)
/// consegue zerar o estoque, criar vendas e quitar o fiado de terceiros.
/// Uma chave única já elimina o acesso anônimo. Se um dia houver mais de um operador,
/// isto vira o ponto de troca para autenticação por usuário.
/// </summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-API-Key";

    /// <summary>Chave esperada. Configurada via <c>API_KEY</c>.</summary>
    public string Key { get; set; } = string.Empty;
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var enviada))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!ChavesConferem(enviada.ToString(), Options.Key))
        {
            Logger.LogWarning("Chave de API inválida em {Method} {Path}.", Request.Method, Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Chave de API inválida."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "pdv-balcao")],
            ApiKeyAuthenticationOptions.Scheme);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuthenticationOptions.Scheme)));
    }

    /// <summary>
    /// Comparação em tempo fixo: <c>==</c> em string sai no primeiro byte diferente e
    /// vaza o tamanho do prefixo correto para quem cronometrar as respostas.
    /// </summary>
    private static bool ChavesConferem(string enviada, string esperada) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(enviada),
            Encoding.UTF8.GetBytes(esperada));
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Commands.Clientes;
using PDV.Application.Queries;

namespace PDV.API.Controllers;

[ApiController]
[Route("api/clientes")]
public sealed class ClientesController(
    CriarClienteHandler criarHandler,
    RegistrarPagamentoFiadoHandler pagamentoHandler,
    ListarClientesQuery listarQuery,
    ListarClientesFiadoQuery listarFiadoQuery,
    HistoricoFiadoQuery historicoQuery) : ControllerBase
{
    /// <summary>Todos os clientes ativos — usado na seleção de cliente.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await listarQuery.Handle(ct));

    /// <summary>Apenas clientes com fiado pendente — usado na tela de fiado.</summary>
    [HttpGet("fiado")]
    public async Task<IActionResult> ListarFiado(CancellationToken ct) =>
        Ok(await listarFiadoQuery.Handle(ct));

    [HttpGet("{clienteId:guid}/fiado")]
    public async Task<IActionResult> Historico(Guid clienteId, CancellationToken ct)
    {
        var extrato = await historicoQuery.Handle(clienteId, ct);
        return extrato is null
            ? NotFound(new { error = "Cliente não encontrado." })
            : Ok(extrato);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarClienteCommand command, CancellationToken ct)
    {
        var result = await criarHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{clienteId:guid}/pagamento")]
    public async Task<IActionResult> RegistrarPagamento(Guid clienteId, [FromBody] PagamentoRequest request, CancellationToken ct)
    {
        var result = await pagamentoHandler.Handle(
            new RegistrarPagamentoFiadoCommand(clienteId, request.Valor), ct);
        return result.IsSuccess ? Ok() : BadRequest(new { error = result.Error });
    }
}

public record PagamentoRequest(
    [Range(LimitesCliente.ValorMinimo, LimitesCliente.ValorMaximo,
        ErrorMessage = "Valor do pagamento deve estar entre R${1} e R${2}.")]
    decimal Valor
);

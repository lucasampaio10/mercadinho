using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Commands.Vendas;
using PDV.Application.Queries;

namespace PDV.API.Controllers;

[ApiController]
[Route("api/vendas")]
public sealed class VendasController(
    IniciarVendaHandler iniciarHandler,
    AdicionarItemHandler adicionarItemHandler,
    AlterarQuantidadeItemHandler alterarQuantidadeHandler,
    RemoverItemHandler removerItemHandler,
    CancelarVendaHandler cancelarHandler,
    FinalizarVendaHandler finalizarHandler,
    ObterVendaQuery obterVendaQuery,
    DashboardQuery dashboardQuery,
    ListarVendasDoDiaQuery listarVendasDoDiaQuery) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(await dashboardQuery.Handle(ct));

    /// <summary>Vendas finalizadas hoje, para a tela "Vendas" — mesmo recorte de dia do dashboard.</summary>
    [HttpGet("hoje")]
    public async Task<IActionResult> Hoje(CancellationToken ct) =>
        Ok(await listarVendasDoDiaQuery.Handle(ct));

    /// <summary>Recupera uma venda e seus itens — usado para restaurar o carrinho após refresh.</summary>
    [HttpGet("{vendaId:guid}")]
    public async Task<IActionResult> Obter(Guid vendaId, CancellationToken ct)
    {
        var venda = await obterVendaQuery.Handle(vendaId, ct);
        return venda is null ? NotFound(new { error = "Venda não encontrada." }) : Ok(venda);
    }

    [HttpPost]
    public async Task<IActionResult> Iniciar(CancellationToken ct)
    {
        var result = await iniciarHandler.Handle(new IniciarVendaCommand(), ct);
        return result.IsSuccess ? Ok(new { vendaId = result.Value }) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{vendaId:guid}/itens")]
    public async Task<IActionResult> AdicionarItem(Guid vendaId, [FromBody] AdicionarItemRequest request, CancellationToken ct)
    {
        var result = await adicionarItemHandler.Handle(
            new AdicionarItemCommand(vendaId, request.ProdutoId, request.Quantidade), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Define a quantidade exata de um item do carrinho — corrigir um "10" digitado
    /// no lugar de "1" não deve exigir remover o item e refazê-lo.
    /// </summary>
    [HttpPut("{vendaId:guid}/itens/{produtoId:guid}")]
    public async Task<IActionResult> AlterarQuantidade(
        Guid vendaId, Guid produtoId, [FromBody] AlterarQuantidadeRequest request, CancellationToken ct)
    {
        var result = await alterarQuantidadeHandler.Handle(
            new AlterarQuantidadeItemCommand(vendaId, produtoId, request.Quantidade), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpDelete("{vendaId:guid}/itens/{produtoId:guid}")]
    public async Task<IActionResult> RemoverItem(Guid vendaId, Guid produtoId, CancellationToken ct)
    {
        var result = await removerItemHandler.Handle(new RemoverItemCommand(vendaId, produtoId), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Descarta uma venda aberta — carrinho abandonado deixa de ficar órfão no banco.</summary>
    [HttpPost("{vendaId:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid vendaId, CancellationToken ct)
    {
        var result = await cancelarHandler.Handle(new CancelarVendaCommand(vendaId), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }

    [HttpPost("{vendaId:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(Guid vendaId, [FromBody] FinalizarVendaRequest request, CancellationToken ct)
    {
        var result = await finalizarHandler.Handle(
            new FinalizarVendaCommand(vendaId, request.FormaPagamento, request.ClienteId, request.Observacao), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}

public record AdicionarItemRequest(
    [Required] Guid ProdutoId,
    [Range(LimitesVenda.QuantidadeMinima, LimitesVenda.QuantidadeMaxima,
        ErrorMessage = "Quantidade deve estar entre {1} e {2}.")]
    int Quantidade
);

public record AlterarQuantidadeRequest(
    [Range(LimitesVenda.QuantidadeMinima, LimitesVenda.QuantidadeMaxima,
        ErrorMessage = "Quantidade deve estar entre {1} e {2}.")]
    int Quantidade
);

public record FinalizarVendaRequest(
    [Required(ErrorMessage = "Forma de pagamento é obrigatória.")]
    [RegularExpression("^(?i:Dinheiro|Cartao|Fiado)$",
        ErrorMessage = "Forma de pagamento deve ser Dinheiro, Cartao ou Fiado.")]
    string FormaPagamento,
    Guid? ClienteId,
    [StringLength(500, ErrorMessage = "Observação deve ter no máximo {1} caracteres.")]
    string? Observacao = null
);

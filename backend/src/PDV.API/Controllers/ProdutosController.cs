using Microsoft.AspNetCore.Mvc;
using PDV.Application.Commands.Produtos;
using PDV.Application.Queries;

namespace PDV.API.Controllers;

[ApiController]
[Route("api/produtos")]
public sealed class ProdutosController(
    CriarProdutoHandler criarHandler,
    AtualizarProdutoHandler atualizarHandler,
    InativarProdutoHandler inativarHandler,
    ListarProdutosQuery listarQuery,
    BuscarProdutoQuery buscarQuery,
    BuscarProdutoPorCodigoBarrasQuery buscarPorCodigoBarrasQuery) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await listarQuery.Handle(ct));

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar([FromQuery] string nome, CancellationToken ct) =>
        Ok(await buscarQuery.Handle(nome, ct));

    [HttpGet("codigo-barras/{codigo}")]
    public async Task<IActionResult> BuscarPorCodigoBarras(string codigo, CancellationToken ct)
    {
        var produto = await buscarPorCodigoBarrasQuery.Handle(codigo, ct);
        return produto is null
            ? NotFound(new { error = "Nenhum produto com esse código de barras." })
            : Ok(produto);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarProdutoCommand command, CancellationToken ct)
    {
        var result = await criarHandler.Handle(command, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarProdutoCommand command, CancellationToken ct)
    {
        var cmd = command with { Id = id };
        var result = await atualizarHandler.Handle(cmd, ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Inativar(Guid id, CancellationToken ct)
    {
        var result = await inativarHandler.Handle(new InativarProdutoCommand(id), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }
}

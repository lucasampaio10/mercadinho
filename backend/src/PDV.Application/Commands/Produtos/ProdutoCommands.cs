using System.ComponentModel.DataAnnotations;
using PDV.Application.Common;
using PDV.Application.DTOs;
using PDV.Domain.Entities;
using PDV.Domain.Interfaces;

namespace PDV.Application.Commands.Produtos;

// ── Limites ───────────────────────────────────────────────────────────────────

/// <summary>
/// Limites aceitos pela API. Espelham as regras de <see cref="Produto"/> — o domínio
/// continua sendo a última palavra; isto só barra o lixo antes de chegar lá.
/// </summary>
public static class LimitesProduto
{
    public const double PrecoMinimo = 0.01;
    public const double PrecoMaximo = 1_000_000;
    public const int EstoqueMaximo = 1_000_000;
    public const int TamanhoNome = 200;
    public const int TamanhoCategoria = 100;
    public const int TamanhoCodigoBarras = 64;
}

// ── Create ────────────────────────────────────────────────────────────────────

public record CriarProdutoCommand(
    [Required(ErrorMessage = "Nome do produto é obrigatório.")]
    [StringLength(LimitesProduto.TamanhoNome, MinimumLength = 1,
        ErrorMessage = "Nome deve ter entre 1 e {1} caracteres.")]
    string Nome,

    [Range(LimitesProduto.PrecoMinimo, LimitesProduto.PrecoMaximo,
        ErrorMessage = "Preço deve estar entre R${1} e R${2}.")]
    decimal Preco,

    [Range(0, LimitesProduto.EstoqueMaximo,
        ErrorMessage = "Estoque deve estar entre {1} e {2}.")]
    int Estoque,

    [StringLength(LimitesProduto.TamanhoCategoria,
        ErrorMessage = "Categoria deve ter no máximo {1} caracteres.")]
    string? Categoria,

    [StringLength(LimitesProduto.TamanhoCodigoBarras,
        ErrorMessage = "Código de barras deve ter no máximo {1} caracteres.")]
    string? CodigoBarras = null
);

public sealed class CriarProdutoHandler(IProdutoRepository repo, IUnitOfWork uow)
{
    /// <summary>Cria um novo produto no catálogo.</summary>
    public async Task<Result<ProdutoResponse>> Handle(CriarProdutoCommand command, CancellationToken ct)
    {
        try
        {
            var produto = Produto.Criar(command.Nome, command.Preco, command.Estoque, command.Categoria, command.CodigoBarras);
            await repo.AddAsync(produto, ct);
            await uow.CommitAsync(ct);

            return Result.Success(produto.ToResponse());
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure<ProdutoResponse>(ex.Message);
        }
    }
}

// ── Update ────────────────────────────────────────────────────────────────────

public record AtualizarProdutoCommand(
    Guid Id,

    [Required(ErrorMessage = "Nome do produto é obrigatório.")]
    [StringLength(LimitesProduto.TamanhoNome, MinimumLength = 1,
        ErrorMessage = "Nome deve ter entre 1 e {1} caracteres.")]
    string Nome,

    [Range(LimitesProduto.PrecoMinimo, LimitesProduto.PrecoMaximo,
        ErrorMessage = "Preço deve estar entre R${1} e R${2}.")]
    decimal Preco,

    [Range(0, LimitesProduto.EstoqueMaximo,
        ErrorMessage = "Estoque deve estar entre {1} e {2}.")]
    int Estoque,

    [StringLength(LimitesProduto.TamanhoCategoria,
        ErrorMessage = "Categoria deve ter no máximo {1} caracteres.")]
    string? Categoria,

    [StringLength(LimitesProduto.TamanhoCodigoBarras,
        ErrorMessage = "Código de barras deve ter no máximo {1} caracteres.")]
    string? CodigoBarras = null
);

public sealed class AtualizarProdutoHandler(IProdutoRepository repo, IUnitOfWork uow)
{
    /// <summary>Atualiza dados de um produto existente.</summary>
    public async Task<Result> Handle(AtualizarProdutoCommand command, CancellationToken ct)
    {
        var produto = await repo.GetByIdAsync(command.Id, ct);
        if (produto is null)
            return Result.Failure("Produto não encontrado.");

        try
        {
            produto.Atualizar(command.Nome, command.Preco, command.Estoque, command.Categoria, command.CodigoBarras);
            repo.Update(produto);
            await uow.CommitAsync(ct);
            return Result.Success();
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}

// ── Delete ────────────────────────────────────────────────────────────────────

public record InativarProdutoCommand(Guid Id);

public sealed class InativarProdutoHandler(IProdutoRepository repo, IUnitOfWork uow)
{
    /// <summary>Inativa (soft delete) um produto do catálogo.</summary>
    public async Task<Result> Handle(InativarProdutoCommand command, CancellationToken ct)
    {
        var produto = await repo.GetByIdAsync(command.Id, ct);
        if (produto is null)
            return Result.Failure("Produto não encontrado.");

        produto.Inativar();
        repo.Update(produto);
        await uow.CommitAsync(ct);
        return Result.Success();
    }
}

// ── Mapper ───────────────────────────────────────────────────────────────────

internal static class ProdutoMapper
{
    internal static ProdutoResponse ToResponse(this Produto p) =>
        new(p.Id, p.Nome, p.Categoria, p.CodigoBarras, p.Preco, p.Estoque, p.Ativo, p.CreatedAt);
}

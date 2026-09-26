using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Item de uma venda — armazena snapshot do preço no momento da venda.
/// </summary>
public sealed class ItemVenda : Entity
{
    public Guid VendaId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string NomeProduto { get; private set; } = string.Empty;
    public decimal PrecoUnitario { get; private set; }
    public int Quantidade { get; private set; }
    public decimal Subtotal => PrecoUnitario * Quantidade;

    private ItemVenda() { } // EF Core

    internal static ItemVenda Criar(Guid vendaId, Produto produto, int quantidade)
    {
        var item = new ItemVenda
        {
            VendaId = vendaId,
            ProdutoId = produto.Id,
            NomeProduto = produto.Nome,
            PrecoUnitario = produto.Preco
        };
        item.DefinirQuantidade(quantidade);
        return item;
    }

    /// <summary>
    /// Teto por item. Existe para que uma quantidade absurda vinda da API
    /// (int.MaxValue) falhe como regra de negócio em vez de estourar o int no Subtotal.
    /// </summary>
    public const int QuantidadeMaxima = 10_000;

    internal void AdicionarQuantidade(int quantidade)
    {
        if (quantidade <= 0)
            throw new DomainException("Quantidade inválida.");

        DefinirQuantidade(Quantidade + (long)quantidade);
    }

    internal void DefinirQuantidade(long quantidade)
    {
        if (quantidade <= 0)
            throw new DomainException("Quantidade inválida.");
        if (quantidade > QuantidadeMaxima)
            throw new DomainException($"Quantidade máxima por item é {QuantidadeMaxima}.");

        Quantidade = (int)quantidade;
        Touch();
    }
}

using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

public enum FormaPagamento { Dinheiro, Cartao, Fiado }
public enum StatusVenda { Aberta, Finalizada, Cancelada }

/// <summary>
/// Aggregate root da Venda. Controla itens e forma de pagamento.
/// </summary>
public sealed class Venda : Entity
{
    public StatusVenda Status { get; private set; } = StatusVenda.Aberta;
    public FormaPagamento? FormaPagamento { get; private set; }
    public Guid? ClienteId { get; private set; } // preenchido quando fiado
    public string? Observacao { get; private set; }
    public decimal Total => _itens.Sum(i => i.Subtotal);

    private readonly List<ItemVenda> _itens = [];
    public IReadOnlyCollection<ItemVenda> Itens => _itens.AsReadOnly();

    private Venda() { } // EF Core

    public static Venda Iniciar() => new();

    /// <summary>
    /// Adiciona ou incrementa um item na venda.
    /// Retorna o novo <see cref="ItemVenda"/> criado, ou <c>null</c> se o item já existia e foi incrementado.
    /// O chamador deve registrar o item retornado no contexto de persistência.
    /// </summary>
    public ItemVenda? AdicionarItem(Produto produto, int quantidade)
    {
        if (Status != StatusVenda.Aberta)
            throw new DomainException("Não é possível alterar uma venda finalizada ou cancelada.");
        if (quantidade <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");

        var existente = _itens.FirstOrDefault(i => i.ProdutoId == produto.Id);
        if (existente is not null)
        {
            existente.AdicionarQuantidade(quantidade);
            Touch();
            return null;
        }

        var novoItem = ItemVenda.Criar(Id, produto, quantidade);
        _itens.Add(novoItem);
        Touch();
        return novoItem;
    }

    /// <summary>
    /// Define a quantidade exata de um item já no carrinho.
    /// Retorna a variação em relação à quantidade anterior: positiva exige debitar
    /// mais estoque, negativa exige devolver. O chamador é quem ajusta o produto.
    /// </summary>
    public int AlterarQuantidadeItem(Guid produtoId, int novaQuantidade)
    {
        if (Status != StatusVenda.Aberta)
            throw new DomainException("Não é possível alterar uma venda finalizada ou cancelada.");
        if (novaQuantidade <= 0)
            throw new DomainException("Quantidade deve ser maior que zero. Para zerar, remova o item.");

        var item = _itens.FirstOrDefault(i => i.ProdutoId == produtoId)
            ?? throw new DomainException("Item não encontrado na venda.");

        var delta = novaQuantidade - item.Quantidade;
        if (delta == 0) return 0;

        item.DefinirQuantidade(novaQuantidade);
        Touch();
        return delta;
    }

    /// <summary>
    /// Remove o item do carrinho e o devolve, para que o chamador saiba
    /// quanto estoque precisa creditar de volta.
    /// </summary>
    public ItemVenda RemoverItem(Guid produtoId)
    {
        if (Status != StatusVenda.Aberta)
            throw new DomainException("Não é possível alterar uma venda finalizada ou cancelada.");

        var item = _itens.FirstOrDefault(i => i.ProdutoId == produtoId)
            ?? throw new DomainException("Item não encontrado na venda.");

        _itens.Remove(item);
        Touch();
        return item;
    }

    public void Finalizar(FormaPagamento formaPagamento, Guid? clienteId = null, string? observacao = null)
    {
        if (Status != StatusVenda.Aberta)
            throw new DomainException("Venda já foi finalizada ou cancelada.");
        if (!_itens.Any())
            throw new DomainException("Não é possível finalizar uma venda sem itens.");
        if (formaPagamento == Entities.FormaPagamento.Fiado && clienteId is null)
            throw new DomainException("Cliente é obrigatório para vendas no fiado.");
        // Sem isso, "Dinheiro + clienteId" gravaria um cliente na venda sem lançar
        // fiado nenhum — a venda apontaria para um devedor que não deve nada.
        if (formaPagamento != Entities.FormaPagamento.Fiado && clienteId is not null)
            throw new DomainException("Cliente só pode ser vinculado a vendas no fiado.");

        FormaPagamento = formaPagamento;
        ClienteId = clienteId;
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        Status = StatusVenda.Finalizada;
        Touch();
    }

    public void Cancelar()
    {
        if (Status == StatusVenda.Finalizada)
            throw new DomainException("Venda finalizada não pode ser cancelada.");

        Status = StatusVenda.Cancelada;
        Touch();
    }
}

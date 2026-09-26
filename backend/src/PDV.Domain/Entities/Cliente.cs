using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Cliente do mercadinho — pode ter saldo de fiado.
/// </summary>
public sealed class Cliente : Entity
{
    public string Nome { get; private set; } = string.Empty;
    public string? Telefone { get; private set; }

    /// <summary>
    /// Cache persistido de <c>HistoricoFiado.Sum(i =&gt; i.Saldo)</c>, mantido por
    /// <see cref="RecalcularSaldo"/> a cada mutação. É coluna (e não propriedade calculada)
    /// porque as queries filtram e ordenam por ela no banco.
    /// </summary>
    public decimal SaldoFiado { get; private set; } = 0;
    public bool Ativo { get; private set; } = true;

    private readonly List<ItemFiado> _historicoFiado = [];
    public IReadOnlyCollection<ItemFiado> HistoricoFiado => _historicoFiado.AsReadOnly();

    private readonly List<Pagamento> _pagamentos = [];
    public IReadOnlyCollection<Pagamento> Pagamentos => _pagamentos.AsReadOnly();

    private Cliente() { } // EF Core

    public static Cliente Criar(string nome, string? telefone = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("Nome do cliente é obrigatório.");

        return new Cliente
        {
            Nome = nome.Trim(),
            Telefone = telefone?.Trim()
        };
    }

    /// <summary>
    /// Adiciona uma compra no fiado. Retorna o novo <see cref="ItemFiado"/> para ser registrado no contexto.
    /// </summary>
    public ItemFiado AdicionarFiado(Guid vendaId, decimal valor)
    {
        if (valor <= 0)
            throw new DomainException("Valor do fiado deve ser maior que zero.");

        var item = ItemFiado.Criar(Id, vendaId, valor);
        _historicoFiado.Add(item);
        RecalcularSaldo();
        Touch();
        return item;
    }

    /// <summary>
    /// Registra pagamento parcial ou total do fiado, abatendo as compras mais antigas
    /// primeiro (FIFO). Um item só é marcado como pago quando seu saldo chega a zero;
    /// o que não fecha um item fica registrado como <c>ValorPago</c> parcial.
    /// Retorna o <see cref="Pagamento"/> criado para ser registrado no contexto.
    /// </summary>
    public Pagamento RegistrarPagamento(decimal valor)
    {
        if (valor <= 0)
            throw new DomainException("Valor do pagamento deve ser maior que zero.");

        // Reconcilia antes de validar: se o saldo persistido divergiu do histórico,
        // o histórico é a fonte da verdade.
        RecalcularSaldo();

        if (valor > SaldoFiado)
            throw new DomainException($"Pagamento R${valor:F2} excede o saldo devedor R${SaldoFiado:F2}.");

        var restante = valor;
        foreach (var item in _historicoFiado.Where(i => !i.Pago).OrderBy(i => i.CreatedAt))
        {
            if (restante <= 0) break;
            restante -= item.Amortizar(restante);
        }

        RecalcularSaldo();
        Touch();

        var pagamento = Pagamento.Criar(Id, valor);
        _pagamentos.Add(pagamento);
        return pagamento;
    }

    public void Atualizar(string nome, string? telefone)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("Nome do cliente é obrigatório.");

        Nome = nome.Trim();
        Telefone = telefone?.Trim();
        Touch();
    }

    private void RecalcularSaldo() => SaldoFiado = _historicoFiado.Sum(i => i.Saldo);
}

using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Representa uma compra no fiado de um cliente.
/// O saldo em aberto do item é <see cref="Valor"/> − <see cref="ValorPago"/>;
/// a soma desses saldos é sempre igual ao <see cref="Cliente.SaldoFiado"/>.
/// </summary>
public sealed class ItemFiado : Entity
{
    public Guid ClienteId { get; private set; }
    public Guid VendaId { get; private set; }
    public decimal Valor { get; private set; }
    public decimal ValorPago { get; private set; }
    public bool Pago { get; private set; } = false;
    public DateTime? PagoEm { get; private set; }

    /// <summary>Quanto ainda falta pagar deste item.</summary>
    public decimal Saldo => Valor - ValorPago;

    private ItemFiado() { } // EF Core

    internal static ItemFiado Criar(Guid clienteId, Guid vendaId, decimal valor)
    {
        if (valor <= 0)
            throw new DomainException("Valor do fiado inválido.");

        return new ItemFiado
        {
            ClienteId = clienteId,
            VendaId = vendaId,
            Valor = valor
        };
    }

    /// <summary>
    /// Abate até <paramref name="disponivel"/> do saldo deste item.
    /// Retorna quanto foi efetivamente abatido — o que sobra segue para o próximo item.
    /// </summary>
    internal decimal Amortizar(decimal disponivel)
    {
        if (disponivel <= 0 || Pago) return 0m;

        var abatido = Math.Min(disponivel, Saldo);
        ValorPago += abatido;

        if (Saldo == 0)
        {
            Pago = true;
            PagoEm = DateTime.UtcNow;
        }

        Touch();
        return abatido;
    }
}

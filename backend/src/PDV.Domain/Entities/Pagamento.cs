using PDV.Domain.Exceptions;

namespace PDV.Domain.Entities;

/// <summary>
/// Um pagamento de fiado recebido do cliente — parcial ou total.
/// Existe para que o histórico mostre o que foi pago, não só o que foi comprado.
/// </summary>
public sealed class Pagamento : Entity
{
    public Guid ClienteId { get; private set; }
    public decimal Valor { get; private set; }

    private Pagamento() { } // EF Core

    internal static Pagamento Criar(Guid clienteId, decimal valor)
    {
        if (valor <= 0)
            throw new DomainException("Valor do pagamento deve ser maior que zero.");

        return new Pagamento
        {
            ClienteId = clienteId,
            Valor = valor
        };
    }
}

namespace PDV.Domain.Entities;

/// <summary>
/// Base de todas as entidades de domínio.
/// Garante identidade única e rastreabilidade temporal.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        // Ids são Guids independentes por tabela — sem o tipo, um Produto e um
        // Cliente que colidissem no Guid seriam considerados iguais.
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

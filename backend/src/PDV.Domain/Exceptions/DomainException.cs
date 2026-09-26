namespace PDV.Domain.Exceptions;

/// <summary>
/// Exceção lançada por violações de regra de negócio no domínio.
/// Não deve ser usada para erros de infraestrutura.
/// </summary>
public sealed class DomainException(string message) : Exception(message);

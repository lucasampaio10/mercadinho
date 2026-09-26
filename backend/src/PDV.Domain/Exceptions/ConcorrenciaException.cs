namespace PDV.Domain.Exceptions;

/// <summary>
/// Outra operação alterou os mesmos registros entre a leitura e a gravação.
/// A camada de infraestrutura traduz o conflito otimista do ORM para esta exceção,
/// para que a Application possa reagir sem conhecer o EF Core.
/// </summary>
public sealed class ConcorrenciaException(string message, Exception? inner = null)
    : Exception(message, inner);

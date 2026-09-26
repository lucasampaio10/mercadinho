namespace PDV.Application.Common;

/// <summary>
/// Result Pattern — evita exceptions para controle de fluxo de negócio.
/// Toda operação que pode falhar por regra de negócio retorna Result.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    protected Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(T value) : base(true, null) => Value = value;
    private Result(string error) : base(false, error) { }

    public static new Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(string error) => new(error);
}

using System.Diagnostics.CodeAnalysis;

namespace BuildingBlocks.Common;

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("A success result cannot carry an error.");
        if (!isSuccess && error is null)
            throw new InvalidOperationException("A failure result must carry an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static implicit operator Result(Error error) => Failure(error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, null) => _value = value;

    private Result(Error error) : base(false, error) => _value = default;

    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException($"Cannot access {nameof(Value)} of a failed result [{Error.Value.Code}].");
            return _value!;
        }
    }

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        if (IsFailure)
            return onFailure(Error.Value);
        return onSuccess(_value!);
    }
}
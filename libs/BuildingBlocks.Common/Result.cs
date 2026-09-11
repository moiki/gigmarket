namespace BuildingBlocks.Common;

public readonly record struct Error(string Code, string Message);

public class Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, Error error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public Error Error { get; }

    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException(
                $"Cannot access {nameof(Value)} of a failed result [{Error.Code}].");

    public static Result<T> Ok(T value) => new(true, value, default);

    public static Result<T> Fail(Error error) => new(false, default, error);
}
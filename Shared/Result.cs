#nullable enable
namespace Shared
{
    /// <summary>
    /// Represents a value that is either a success (<see cref="Ok"/>) or a failure (<see cref="Err"/>).
    /// Use this instead of throwing exceptions for expected operational failures.
    /// </summary>
    public readonly struct Result<T, TError>
    {
        public bool IsOk { get; }
        public bool IsError => !IsOk;
        public T Value { get; }
        public TError Error { get; }

        private Result(T value)
        {
            IsOk = true;
            Value = value;
            Error = default!;
        }

        private Result(TError error)
        {
            IsOk = false;
            Value = default!;
            Error = error;
        }

        public static Result<T, TError> Ok(T value) => new Result<T, TError>(value);
        public static Result<T, TError> Err(TError error) => new Result<T, TError>(error);

        public override string ToString() =>
            IsOk ? $"Ok({Value})" : $"Err({Error})";
    }

    /// <summary>
    /// A unit type representing a successful result with no meaningful value.
    /// </summary>
    public readonly struct Unit
    {
        public static readonly Unit Value = default;
        public override string ToString() => "()";
    }
}

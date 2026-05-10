#nullable enable
using System;

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

        public TResult Match<TResult>(Func<T, TResult> ok, Func<TError, TResult> err)
        {
            if (ok == null) throw new ArgumentNullException(nameof(ok));
            if (err == null) throw new ArgumentNullException(nameof(err));

            return IsOk ? ok(Value) : err(Error);
        }

        public Result<TOut, TError> Map<TOut>(Func<T, TOut> map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return IsOk
                ? Result<TOut, TError>.Ok(map(Value))
                : Result<TOut, TError>.Err(Error);
        }

        public Result<T, TOutError> MapError<TOutError>(Func<TError, TOutError> map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return IsOk
                ? Result<T, TOutError>.Ok(Value)
                : Result<T, TOutError>.Err(map(Error));
        }

        public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> bind)
        {
            if (bind == null) throw new ArgumentNullException(nameof(bind));
            return IsOk ? bind(Value) : Result<TOut, TError>.Err(Error);
        }

        public Result<T, TError> Tap(Action<T> tap)
        {
            if (tap == null) throw new ArgumentNullException(nameof(tap));
            if (IsOk)
                tap(Value);
            return this;
        }

        public Result<T, TError> TapError(Action<TError> tap)
        {
            if (tap == null) throw new ArgumentNullException(nameof(tap));
            if (IsError)
                tap(Error);
            return this;
        }

        public bool TryGetValue(out T value, out TError error)
        {
            if (IsOk)
            {
                value = Value;
                error = default!;
                return true;
            }

            value = default!;
            error = Error;
            return false;
        }

        public T ValueOr(T fallback) => IsOk ? Value : fallback;

        public T ValueOr(Func<TError, T> fallback)
        {
            if (fallback == null) throw new ArgumentNullException(nameof(fallback));
            return IsOk ? Value : fallback(Error);
        }

        public Result<T, TError> Ensure(Func<T, bool> predicate, Func<TError> errorFactory)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (errorFactory == null) throw new ArgumentNullException(nameof(errorFactory));

            if (IsError)
                return this;

            return predicate(Value) ? this : Err(errorFactory());
        }

        public override string ToString() =>
            IsOk ? $"Ok({Value})" : $"Err({Error})";
    }

    /// <summary>
    /// Convenience result shape for success-without-value operations.
    /// </summary>
    public readonly struct Result<TError>
    {
        private readonly Result<Unit, TError> _inner;

        public bool IsOk => _inner.IsOk;
        public bool IsError => _inner.IsError;
        public TError Error => _inner.Error;

        private Result(Result<Unit, TError> inner)
        {
            _inner = inner;
        }

        public static Result<TError> Ok() => new Result<TError>(Result<Unit, TError>.Ok(Unit.Value));
        public static Result<TError> Err(TError error) => new Result<TError>(Result<Unit, TError>.Err(error));

        public Result<Unit, TError> AsUnitResult() => _inner;

        public override string ToString() => _inner.ToString();
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

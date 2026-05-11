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
        private readonly T _value;
        private readonly TError _error;

        public bool IsOk { get; }
        public bool IsError => !IsOk;
        public T Value => IsOk
            ? _value
            : default!;
        public TError Error => IsError
            ? _error
            : default!;

        private Result(T value)
        {
            IsOk = true;
            _value = value;
            _error = default!;
        }

        private Result(TError error)
        {
            IsOk = false;
            _value = default!;
            _error = error;
        }

        public static Result<T, TError> Ok(T value) => new Result<T, TError>(value);
        public static Result<T, TError> Err(TError error) => new Result<T, TError>(error);

        public TResult Match<TResult>(Func<T, TResult> ok, Func<TError, TResult> err)
        {
            if (ok == null || err == null)
                return default!;

            return IsOk ? ok(_value) : err(_error);
        }

        public Result<TOut, TError> Map<TOut>(Func<T, TOut> map)
        {
            if (map == null)
                return Result<TOut, TError>.Err(_error);
            return IsOk
                ? Result<TOut, TError>.Ok(map(_value))
                : Result<TOut, TError>.Err(_error);
        }

        public Result<T, TOutError> MapError<TOutError>(Func<TError, TOutError> map)
        {
            if (map == null)
                return IsOk ? Result<T, TOutError>.Ok(_value) : Result<T, TOutError>.Err(default!);
            return IsOk
                ? Result<T, TOutError>.Ok(_value)
                : Result<T, TOutError>.Err(map(_error));
        }

        public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> bind)
        {
            if (bind == null)
                return Result<TOut, TError>.Err(_error);
            return IsOk ? bind(_value) : Result<TOut, TError>.Err(_error);
        }

        public Result<T, TError> Tap(Action<T> tap)
        {
            if (IsOk && tap != null)
                tap(_value);
            return this;
        }

        public Result<T, TError> TapError(Action<TError> tap)
        {
            if (IsError && tap != null)
                tap(_error);
            return this;
        }

        public bool TryGetValue(out T value, out TError error)
        {
            if (IsOk)
            {
                value = _value;
                error = default!;
                return true;
            }

            value = default!;
            error = _error;
            return false;
        }

        public T ValueOr(T fallback) => IsOk ? _value : fallback;

        public T ValueOr(Func<TError, T> fallback)
        {
            if (fallback == null)
                return default!;
            return IsOk ? _value : fallback(_error);
        }

        public Result<T, TError> Ensure(Func<T, bool> predicate, Func<TError> errorFactory)
        {
            if (predicate == null || errorFactory == null)
                return this;

            if (IsError)
                return this;

            return predicate(_value) ? this : Err(errorFactory());
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

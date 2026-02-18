using System.Diagnostics.CodeAnalysis;
using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Domain;

// Result is a specialized Either where the second option is an Error
// By specifying that the second option is Error, we can have some methods which
// are fluent in their error-handling and consideration.
public static class Result
{
    public static Result<T, TE1> FromValue<T, TE1>(T value)
        => new Result<T, TE1>(value, default, 0);

    public static Result<T, TE1> FromError<T, TE1>(TE1 error)
        => new Result<T, TE1>(default, error, 1);

    [DoesNotReturn]
    public static T ThrowResultInvalidException<T>()
        => throw new InvalidOperationException("Result is in an invalid state");

    public static Result<T2, T1> Invert<T1, T2>(this Result<T1, T2> result)
        => result.Match(
            t1 => new Result<T2, T1>(default, t1, 1),
            t2 => new Result<T2, T1>(t2, default, 0));

    public static Result<T, TE1> Create<T, TE1>(T value)
        => new Result<T, TE1>(value, default, 0);

    public static Result<T, TE1> Create<T, TE1>(TE1 error1)
        => new Result<T, TE1>(default, error1, 1);

    public static Result<T, Exception> Try<T>(Func<T> function)
    {
        try
        {
            return function();
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    public static Result<T, Exception> Try<T, TData>(TData data, Func<TData, T> function)
    {
        try
        {
            return function(data);
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}

public readonly record struct Result<T, TE1> : IEquatable<T>
{
    private readonly int _index;
    private readonly T? _value;
    private readonly TE1? _error;

    public Result(T? value, TE1? error, int index)
    {
        _index = index;
        if (index == 0)
            _value = NotNull(value);
        else if (index == 1)
            _error = NotNull(error);
    }

    public bool IsSuccess => _index == 0 && _value is not null;
    public bool IsError => _index == 1 && _error is not null;
    public bool IsValid => IsSuccess || IsError;

    public static implicit operator Result<T, TE1>(T value)
        => new Result<T, TE1>(value, default, 0);

    public static implicit operator Result<T, TE1>(TE1 error)
        => new Result<T, TE1>(default, error, 1);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TE1, TOut> onError)
    {
        if (_index == 0 && _value is not null)
            return NotNull(onSuccess)(_value!);
        if (_index == 1 && _error is not null)
            return NotNull(onError)(_error!);
        return Result.ThrowResultInvalidException<TOut>();
    }

    public TOut Match<TOut, TData>(TData data, Func<T, TData, TOut> onSuccess, Func<TE1, TData, TOut> onError)
    {
        if (_index == 0 && _value is not null)
            return NotNull(onSuccess)(_value!, data);
        if (_index == 1 && _error is not null)
            return NotNull(onError)(_error!, data);
        return Result.ThrowResultInvalidException<TOut>();
    }

    public void Switch(Action<T> onSuccess, Action<TE1> onError)
    {
        if (_index == 0 && _value is not null)
        {
            NotNull(onSuccess)(_value!);
            return;
        }
        if (_index == 1 && _error is not null)
        {
            NotNull(onError)(_error!);
            return;
        }
        Result.ThrowResultInvalidException<int>();
    }

    public void Switch<TData>(TData data, Action<T, TData> onSuccess, Action<TE1, TData> onError)
    {
        if (_index == 0 && _value is not null)
        {
            NotNull(onSuccess)(_value!, data);
            return;
        }
        if (_index == 1 && _error is not null)
        {
            NotNull(onError)(_error!, data);
            return;
        }
        Result.ThrowResultInvalidException<int>();
    }

    public Result<TOut, TE1> Bind<TOut>(Func<T, Result<TOut, TE1>> func)
        => Match(NotNull(func), static (v, f) => f(v), static (e, _) => e);

    public Result<TOut, TE1> Bind<TData, TOut>(TData data, Func<T, TData, Result<TOut, TE1>> func)
        => Match(
            (func: NotNull(func), data),
            static (v, d) => d.func(v, d.data),
            static (e, _) => e);

    // Synonym for "Bind", but probably easier to read
    public Result<TOut, TE1> And<TOut>(Func<T, Result<TOut, TE1>> func)
        => Match(NotNull(func), static (v, f) => f(v), static (e, _) => e);

    // If this result fails, take the second result. Use the error type of the second result in
    // either case.
    public Result<T, TError2> Or<TError2>(Func<TE1, Result<T, TError2>> func)
        => Match(static v => new Result<T, TError2>(v, default, 0), func);

    public Result<T, TE1> If(Func<T, bool> predicate, Func<T, Result<T, TE1>> then)
        => Bind((predicate, then, result: this), (v, d) => d.predicate(v) ? then(v) : d.result);

    // Map the success result value
    public Result<TOut, TE1> Map<TOut>(Func<T, TOut> map)
        => Match(
            NotNull(map),
            static (v, m) => new Result<TOut, TE1>(m(v), default, 0),
            static (e, _) => new Result<TOut, TE1>(default, e, 1));

    // Map the error result value.
    public Result<T, TErrorOut> MapError<TErrorOut>(Func<TE1, TErrorOut> map)
        => Match(
            NotNull(map),
            static (v, _) => new Result<T, TErrorOut>(v, default, 0),
            static (e, m) => new Result<T, TErrorOut>(default, m(e), 1));

    public Result<T, TE1> OnSuccess(Action<T> onSuccess)
    {
        Switch(onSuccess, static _ => { });
        return this;
    }

    public Result<T, TE1> OnError(Action<TE1> onError)
    {
        Switch(static _ => { }, onError);
        return this;
    }

    public T GetValueOrDefault(T defaultValue)
        => Match(defaultValue, static (t, _) => t, static (_, d) => d);

    public TE1 GetErrorOrDefault(TE1 defaultValue)
        => Match(defaultValue, static (_, d) => d, static (e, _) => e);

    public bool Is(T expected)
        => expected is not null && Match(expected, static (v, e) => v!.Equals(e), static (_, _) => false);

    public bool Is(Func<T, bool> predicate)
        => Match(predicate, static _ => false);

    public bool Equals(T? other)
        => other is not null && Match(
            other,
            static (v, o) => v!.Equals(o),
            static (_, _) => false);
}

using System.Diagnostics;

namespace MeetingReminder.Domain;

public abstract record Error(string Message, string StackTrace)
{
    public Error(string message)
        : this(message, GetCurrentStackTrace())
    {
    }

    public static string GetCurrentStackTrace() => new StackTrace(1, true).ToString();

    public static Error Flatten(IReadOnlyList<Error> errors)
    {
        var allErrors = new List<Error>();
        FlattenTo(errors, allErrors);
        return allErrors switch
        {
            [] => new UnknownError("Error not specified"),
            [var error] => error,
            _ => new AggregateError(allErrors)
        };
    }

    private static void FlattenTo(IReadOnlyList<Error> source, List<Error> target)
    {
        foreach (var error in source)
        {
            if (error is AggregateError aggregate)
                FlattenTo(aggregate.Errors, target);
            else
                target.Add(error);
        }
    }
}

public sealed record AggregateError(IReadOnlyList<Error> Errors)
    : Error(BuildMessage(Errors))
{
    private static string BuildMessage(IReadOnlyList<Error> errors)
        => errors switch
        {
            [] => "Error not specified",
            [var error] => error.Message,
            _ => $"{errors.Count} errors: {string.Join("; ", errors.Select(e => e.Message))}"
        };
}

public sealed record UnknownException(Exception Exception)
    : Error($"Unknown exception: {Exception.GetType().Name} {Exception.Message}", Exception.StackTrace ?? GetCurrentStackTrace());

public sealed record UnknownError(string Message)
    : Error($"Unknown error: {Message}");

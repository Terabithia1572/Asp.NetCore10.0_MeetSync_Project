namespace MeetSync.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.")
    {
    }
}

public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base($"One or more validation errors occurred: {FormatErrors(errors)}")
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    private static string FormatErrors(IDictionary<string, string[]>? errors)
    {
        if (errors == null || errors.Count == 0) return "No failure details provided.";
        return string.Join("; ", errors.Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}"));
    }
}

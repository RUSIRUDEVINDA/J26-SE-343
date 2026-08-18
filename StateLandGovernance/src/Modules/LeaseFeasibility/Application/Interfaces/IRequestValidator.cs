using System;
using System.Collections.Generic;
using System.Linq;

namespace StateLandGovernance.LeaseFeasibility.Application.Interfaces;

public interface IRequestValidator<in TRequest>
{
    ValidationResult Validate(TRequest request);
}

public sealed class ValidationResult
{
    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    private ValidationResult(IReadOnlyList<string> errors) => Errors = errors;

    public static ValidationResult Success() => new(Array.Empty<string>());

    public static ValidationResult Failure(params string[] errors) => new(errors);

    public static ValidationResult Failure(IEnumerable<string> errors) => new(errors.ToList());
}

public sealed class ValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public ValidationException(IReadOnlyList<string> errors)
        : base(string.Join("; ", errors))
    {
        Errors = errors;
    }
}

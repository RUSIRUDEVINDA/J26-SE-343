namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Representation of a fact value with its kind (e.g. Text, Integer, Decimal, Date, Boolean, Identifier)
/// and canonical string representation.
/// </summary>
public sealed record FactValueDto(
    string Kind,
    string CanonicalValue
);

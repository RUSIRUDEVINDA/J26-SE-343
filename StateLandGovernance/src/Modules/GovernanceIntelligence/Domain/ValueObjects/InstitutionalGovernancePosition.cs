using System;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

public sealed record InstitutionalGovernancePosition
{
    public string InstitutionId { get; }
    public InstitutionalPositionType Position { get; }
    public string? AuthorityRole { get; }
    public string? ReasonCode { get; }
    public string? SummaryNotes { get; }
    public DateTime? SubmittedTimestamp { get; }

    public InstitutionalGovernancePosition(
        string institutionId,
        InstitutionalPositionType position,
        string? authorityRole = null,
        string? reasonCode = null,
        string? summaryNotes = null,
        DateTime? submittedTimestamp = null)
    {
        if (string.IsNullOrWhiteSpace(institutionId))
        {
            throw new ArgumentException("Institution ID cannot be null or empty.", nameof(institutionId));
        }

        InstitutionId = institutionId.Trim().ToUpperInvariant();
        Position = position;
        AuthorityRole = authorityRole?.Trim();
        ReasonCode = reasonCode?.Trim();
        SummaryNotes = summaryNotes?.Trim();
        SubmittedTimestamp = submittedTimestamp;
    }
}

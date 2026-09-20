using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Immutable input record representing a single observed case concern indicator.
/// Does not contain applicant PII, national identity numbers, officer identifiers, or complaint narratives.
/// </summary>
public sealed record EarlyGovernanceIndicatorRecord
{
    public EarlyGovernanceIndicatorType IndicatorType { get; }
    public EarlyGovernanceEvidenceState EvidenceState { get; }
    public string? EvidenceReference { get; }
    public DateTimeOffset? RecordedAtUtc { get; }

    public EarlyGovernanceIndicatorRecord(
        EarlyGovernanceIndicatorType indicatorType,
        EarlyGovernanceEvidenceState evidenceState,
        string? evidenceReference = null,
        DateTimeOffset? recordedAtUtc = null)
    {
        if (!Enum.IsDefined(typeof(EarlyGovernanceIndicatorType), indicatorType))
        {
            throw new ArgumentException($"Undefined indicator type: {(int)indicatorType}.", nameof(indicatorType));
        }

        if (!Enum.IsDefined(typeof(EarlyGovernanceEvidenceState), evidenceState))
        {
            throw new ArgumentException($"Undefined evidence state: {(int)evidenceState}.", nameof(evidenceState));
        }

        // If a timestamp is provided for any state, it must have zero UTC offset
        if (recordedAtUtc.HasValue && recordedAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException($"Supplied timestamp for indicator '{indicatorType}' must have zero UTC offset.", nameof(recordedAtUtc));
        }

        // Provenance requirements: VerifiedPresent, VerifiedAbsent, and NotApplicable require evidence reference and timestamp
        bool requiresProvenance = evidenceState is EarlyGovernanceEvidenceState.VerifiedPresent
            or EarlyGovernanceEvidenceState.VerifiedAbsent
            or EarlyGovernanceEvidenceState.NotApplicable;

        if (requiresProvenance)
        {
            if (string.IsNullOrWhiteSpace(evidenceReference))
            {
                throw new ArgumentException($"Indicator '{indicatorType}' in state '{evidenceState}' requires a non-empty evidence reference.", nameof(evidenceReference));
            }

            if (!recordedAtUtc.HasValue)
            {
                throw new ArgumentException($"Indicator '{indicatorType}' in state '{evidenceState}' requires a recorded UTC timestamp.", nameof(recordedAtUtc));
            }
        }

        IndicatorType = indicatorType;
        EvidenceState = evidenceState;
        EvidenceReference = string.IsNullOrWhiteSpace(evidenceReference) ? null : evidenceReference.Trim();
        RecordedAtUtc = recordedAtUtc;
    }
}

/// <summary>
/// Immutable snapshot input for early governance screening of a land lease case.
/// </summary>
public sealed record EarlyGovernanceScreeningInput
{
    public string CaseId { get; }
    public string InputVersion { get; }
    public IReadOnlyList<EarlyGovernanceIndicatorRecord> Indicators { get; }

    public EarlyGovernanceScreeningInput(
        string caseId,
        string inputVersion,
        IEnumerable<EarlyGovernanceIndicatorRecord> indicators)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("CaseId cannot be null or whitespace.", nameof(caseId));
        }

        if (string.IsNullOrWhiteSpace(inputVersion))
        {
            throw new ArgumentException("InputVersion cannot be null or whitespace.", nameof(inputVersion));
        }

        if (indicators is null)
        {
            throw new ArgumentNullException(nameof(indicators), "Indicators collection cannot be null.");
        }

        var indicatorList = new List<EarlyGovernanceIndicatorRecord>();
        var seenTypes = new HashSet<EarlyGovernanceIndicatorType>();

        foreach (var item in indicators)
        {
            if (item is null)
            {
                throw new ArgumentException("Indicators collection contains null elements.", nameof(indicators));
            }

            if (!seenTypes.Add(item.IndicatorType))
            {
                throw new ArgumentException($"Duplicate indicator type '{item.IndicatorType}' detected.", nameof(indicators));
            }

            indicatorList.Add(item);
        }

        CaseId = caseId.Trim();
        InputVersion = inputVersion.Trim();
        Indicators = indicatorList.AsReadOnly();
    }
}

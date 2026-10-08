using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service implementation of the early governance screening engine.
/// Evaluates recorded case concerns deterministically without I/O, random IDs, or clock dependencies.
/// </summary>
public sealed class EarlyGovernanceScreeningEngine : IEarlyGovernanceScreeningEngine
{
    private static readonly EarlyGovernanceIndicatorType[] SupportedIndicatorsInOrder =
    [
        EarlyGovernanceIndicatorType.LegalDispute,
        EarlyGovernanceIndicatorType.UnauthorizedOccupation,
        EarlyGovernanceIndicatorType.UnauthorizedConstruction,
        EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim,
        EarlyGovernanceIndicatorType.MultipleClaimants,
        EarlyGovernanceIndicatorType.UnresolvedObjection,
        EarlyGovernanceIndicatorType.PreviousIllegalLandActivity
    ];

    public EarlyGovernanceScreeningResult Screen(EarlyGovernanceScreeningInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input), "Screening input cannot be null.");
        }

        // Map input records by IndicatorType
        var inputMap = input.Indicators.ToDictionary(i => i.IndicatorType);

        var results = new List<EarlyGovernanceIndicatorResult>(SupportedIndicatorsInOrder.Length);

        foreach (var indicatorType in SupportedIndicatorsInOrder)
        {
            if (inputMap.TryGetValue(indicatorType, out var record))
            {
                bool requiresReview = record.EvidenceState == EarlyGovernanceEvidenceState.VerifiedPresent;
                bool needsEvidence = record.EvidenceState is EarlyGovernanceEvidenceState.Unverified
                    or EarlyGovernanceEvidenceState.Missing
                    or EarlyGovernanceEvidenceState.Unavailable;

                string reasonCode = GetReasonCode(indicatorType, record.EvidenceState);
                string message = GetMessage(indicatorType, record.EvidenceState);

                results.Add(new EarlyGovernanceIndicatorResult(
                    indicatorType,
                    record.EvidenceState,
                    requiresReview,
                    needsEvidence,
                    reasonCode,
                    message,
                    record.EvidenceReference,
                    record.RecordedAtUtc));
            }
            else
            {
                // Omitted indicator defaults to Missing
                var missingState = EarlyGovernanceEvidenceState.Missing;
                string reasonCode = GetReasonCode(indicatorType, missingState);
                string message = GetMessage(indicatorType, missingState);

                results.Add(new EarlyGovernanceIndicatorResult(
                    indicatorType,
                    missingState,
                    requiresReview: false,
                    needsEvidence: true,
                    reasonCode,
                    message,
                    evidenceReference: null,
                    recordedAtUtc: null));
            }
        }

        // OverallStatus precedence:
        // 1. If ANY indicator RequiresReview -> ReviewRequired
        // 2. Otherwise, if ANY indicator NeedsEvidence -> InsufficientInformation
        // 3. Otherwise -> Clear
        EarlyGovernanceScreeningStatus overallStatus;
        if (results.Any(r => r.RequiresReview))
        {
            overallStatus = EarlyGovernanceScreeningStatus.ReviewRequired;
        }
        else if (results.Any(r => r.NeedsEvidence))
        {
            overallStatus = EarlyGovernanceScreeningStatus.InsufficientInformation;
        }
        else
        {
            overallStatus = EarlyGovernanceScreeningStatus.Clear;
        }

        // HasIncompleteEvidence is true whenever ANY indicator NeedsEvidence, even when ReviewRequired
        bool hasIncompleteEvidence = results.Any(r => r.NeedsEvidence);

        return new EarlyGovernanceScreeningResult(
            input.CaseId,
            input.InputVersion,
            overallStatus,
            hasIncompleteEvidence,
            results);
    }

    private static string GetReasonCode(EarlyGovernanceIndicatorType type, EarlyGovernanceEvidenceState state)
    {
        var typeToken = type switch
        {
            EarlyGovernanceIndicatorType.LegalDispute => "LEGAL_DISPUTE",
            EarlyGovernanceIndicatorType.UnauthorizedOccupation => "UNAUTHORIZED_OCCUPATION",
            EarlyGovernanceIndicatorType.UnauthorizedConstruction => "UNAUTHORIZED_CONSTRUCTION",
            EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim => "FAMILY_INHERITANCE_CLAIM",
            EarlyGovernanceIndicatorType.MultipleClaimants => "MULTIPLE_CLAIMANTS",
            EarlyGovernanceIndicatorType.UnresolvedObjection => "UNRESOLVED_OBJECTION",
            EarlyGovernanceIndicatorType.PreviousIllegalLandActivity => "PREVIOUS_ILLEGAL_ACTIVITY",
            _ => "UNKNOWN_CONCERN"
        };

        var stateToken = state switch
        {
            EarlyGovernanceEvidenceState.VerifiedPresent => "VERIFIED_PRESENT",
            EarlyGovernanceEvidenceState.VerifiedAbsent => "VERIFIED_ABSENT",
            EarlyGovernanceEvidenceState.Unverified => "UNVERIFIED",
            EarlyGovernanceEvidenceState.Missing => "MISSING",
            EarlyGovernanceEvidenceState.Unavailable => "UNAVAILABLE",
            EarlyGovernanceEvidenceState.NotApplicable => "NOT_APPLICABLE",
            _ => "UNKNOWN_STATE"
        };

        return $"EG_{typeToken}_{stateToken}";
    }

    private static string GetMessage(EarlyGovernanceIndicatorType type, EarlyGovernanceEvidenceState state)
    {
        if (state == EarlyGovernanceEvidenceState.VerifiedPresent)
        {
            return type switch
            {
                EarlyGovernanceIndicatorType.LegalDispute =>
                    "Verified case evidence records a legal dispute. Officer review is required.",
                EarlyGovernanceIndicatorType.UnauthorizedOccupation =>
                    "Verified case evidence records unauthorized occupation. Officer review is required.",
                EarlyGovernanceIndicatorType.UnauthorizedConstruction =>
                    "Verified case evidence records unauthorized construction. Officer review is required.",
                EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim =>
                    "Verified case evidence records a family or inheritance claim. Officer review is required.",
                EarlyGovernanceIndicatorType.MultipleClaimants =>
                    "Verified case evidence records multiple claimants. Officer review is required.",
                EarlyGovernanceIndicatorType.UnresolvedObjection =>
                    "Verified case evidence records an unresolved objection. Officer review is required.",
                EarlyGovernanceIndicatorType.PreviousIllegalLandActivity =>
                    "Verified case evidence records previous illegal land activity. Officer review is required.",
                _ => "Verified case evidence records a case concern. Officer review is required."
            };
        }

        var concernDisplay = type switch
        {
            EarlyGovernanceIndicatorType.LegalDispute => "legal dispute",
            EarlyGovernanceIndicatorType.UnauthorizedOccupation => "unauthorized occupation",
            EarlyGovernanceIndicatorType.UnauthorizedConstruction => "unauthorized construction",
            EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim => "family or inheritance claim",
            EarlyGovernanceIndicatorType.MultipleClaimants => "multiple claimants",
            EarlyGovernanceIndicatorType.UnresolvedObjection => "unresolved objection",
            EarlyGovernanceIndicatorType.PreviousIllegalLandActivity => "previous illegal land activity",
            _ => "case concern"
        };

        return state switch
        {
            EarlyGovernanceEvidenceState.VerifiedAbsent =>
                $"Verified case evidence records no {concernDisplay} for this assessment.",
            EarlyGovernanceEvidenceState.NotApplicable =>
                $"Authorized assessment records that {concernDisplay} is not applicable for this case.",
            EarlyGovernanceEvidenceState.Unverified =>
                $"Information regarding {concernDisplay} is supplied but unverified. Evidence verification is required.",
            EarlyGovernanceEvidenceState.Missing =>
                $"No evidence regarding {concernDisplay} has been supplied. Required evidence is missing.",
            EarlyGovernanceEvidenceState.Unavailable =>
                $"Evidence regarding {concernDisplay} could not be obtained. Required evidence is unavailable.",
            _ => $"Assessment status for {concernDisplay} is unrecognized."
        };
    }
}

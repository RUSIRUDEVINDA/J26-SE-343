using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Domain.Services;

/// <summary>
/// Domain service implementation of the governance conflict detection engine.
/// </summary>
public sealed class GovernanceConflictEngine : IGovernanceConflictEngine
{
    public IReadOnlyList<DetectedConflict> DetectConflicts(
        IReadOnlyList<GovernanceDecisionSnapshot> decisions,
        DateTime evaluationTimestamp)
    {
        if (decisions is null)
        {
            throw new ArgumentNullException(nameof(decisions));
        }

        var utcTimestamp = evaluationTimestamp.Kind == DateTimeKind.Utc ? evaluationTimestamp : evaluationTimestamp.ToUniversalTime();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Approval", "Rejection", "Restriction", "Condition" };
        var allowedLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "National", "Provincial", "Local" };

        // 1. Boundary & Invariant Validation
        foreach (var decision in decisions)
        {
            if (decision is null)
            {
                throw new ArgumentException("Input decision list contains null elements.");
            }

            if (string.IsNullOrWhiteSpace(decision.DecisionId))
            {
                throw new ArgumentException("Decision identifier cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(decision.SubjectId))
            {
                throw new ArgumentException("Subject identifier cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(decision.InstitutionName))
            {
                throw new ArgumentException("Institution identifier cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(decision.DecisionType) || !allowedTypes.Contains(decision.DecisionType.Trim()))
            {
                throw new ArgumentException($"Decision type '{decision.DecisionType}' is unsupported.");
            }

            if (string.IsNullOrWhiteSpace(decision.AuthorityLevel) || !allowedLevels.Contains(decision.AuthorityLevel.Trim()))
            {
                throw new ArgumentException($"Authority level '{decision.AuthorityLevel}' is unsupported.");
            }

            if (decision.EffectiveFrom > decision.EffectiveTo)
            {
                throw new ArgumentException($"Decision {decision.DecisionId} has an invalid date range: EffectiveFrom cannot be after EffectiveTo.");
            }

            var normId = Normalize(decision.DecisionId);
            if (!seenIds.Add(normId))
            {
                throw new ArgumentException($"Duplicate decision ID '{decision.DecisionId}' detected.");
            }
        }

        var conflicts = new List<DetectedConflict>();

        // Group decisions by SubjectId (parcel or lease)
        var groupedDecisions = decisions.GroupBy(d => Normalize(d.SubjectId));

        foreach (var group in groupedDecisions)
        {
            var subjectId = group.Key;
            var list = group.ToList();

            // Pairwise comparison
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];

                    // Check for temporal overlap (precondition)
                    if (HasTemporalOverlap(a, b))
                    {
                        EvaluatePairwiseConflicts(a, b, subjectId, utcTimestamp, conflicts);
                    }
                }
            }
        }

        // Return conflicts sorted deterministically by stable logical fields
        return conflicts
            .OrderBy(c => GetSeverityRank(c.Severity))
            .ThenBy(c => c.ConflictType, StringComparer.Ordinal)
            .ThenBy(c => c.ConflictId, StringComparer.Ordinal)
            .ToList();
    }

    private static bool HasTemporalOverlap(GovernanceDecisionSnapshot a, GovernanceDecisionSnapshot b)
    {
        // Interval boundary overlap: including endpoints overlap
        return a.EffectiveFrom <= b.EffectiveTo && b.EffectiveFrom <= a.EffectiveTo;
    }

    private static string Normalize(string val)
    {
        return (val ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string GenerateDeterministicConflictId(
        string category,
        string subjectId,
        IReadOnlyList<string> decisionIds)
    {
        var normCategory = Normalize(category);
        var normSubject = Normalize(subjectId);
        var normDecisions = decisionIds
            .Select(Normalize)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        // Length-prefixed serialization format: Length(Comp):Comp\n
        var builder = new System.Text.StringBuilder();
        builder.Append($"{normCategory.Length}:{normCategory}\n");
        builder.Append($"{normSubject.Length}:{normSubject}\n");
        foreach (var dec in normDecisions)
        {
            builder.Append($"{dec.Length}:{dec}\n");
        }

        var serializedBytes = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha.ComputeHash(serializedBytes);

        var hexDigest = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();

        var prefix = category switch
        {
            "ContradictoryDecisions" => "CONF_CONTRADICT",
            "AuthorityLevelInconsistency" => "CONF_AUTHORITY",
            "RegulatoryConflict" => "CONF_REGULATORY",
            "MandateOverlap" => "CONF_MANDATE",
            "LandUseIncompatibility" => "CONF_LANDUSE",
            "DuplicateAllocation" => "CONF_DUP_ALLOC",
            "ConflictingStatuses" => "CONF_STATUS",
            "OverlappingLeasePeriod" => "CONF_OVERLAP_LEASE",
            _ => "CONF_CONFLICT"
        };

        return $"{prefix}_{hexDigest}";
    }

    private static string GetGeometryDisclaimer(GovernanceDecisionSnapshot a, GovernanceDecisionSnapshot b)
    {
        if (string.IsNullOrWhiteSpace(a.ParcelGeometry) || string.IsNullOrWhiteSpace(b.ParcelGeometry))
        {
            return " (Note: Reliable parcel geometry is unavailable; conflict identified via cadastral/subject identifier collision. Spatial overlap cannot be claimed without verified parcel geometry.)";
        }

        return " (Parcel geometry records attached for administrative boundary verification.)";
    }

    private static void EvaluatePairwiseConflicts(
        GovernanceDecisionSnapshot a,
        GovernanceDecisionSnapshot b,
        string subjectId,
        DateTime evaluationTimestamp,
        List<DetectedConflict> conflicts)
    {
        string typeA = Normalize(a.DecisionType);
        string typeB = Normalize(b.DecisionType);

        var rawDecisionIds = new[] { a.DecisionId, b.DecisionId };
        var sortedDecisionIds = rawDecisionIds.OrderBy(Normalize, StringComparer.Ordinal).ToList();
        var sortedInstitutions = new[] { a.InstitutionName, b.InstitutionName }.OrderBy(Normalize, StringComparer.Ordinal).ToList();
        var geometryDisclaimer = GetGeometryDisclaimer(a, b);

        // 1. Contradictory Decisions Check
        if ((typeA == "APPROVAL" && typeB == "REJECTION") || (typeA == "REJECTION" && typeB == "APPROVAL"))
        {
            int rankA = GetAuthorityRank(a.AuthorityLevel);
            int rankB = GetAuthorityRank(b.AuthorityLevel);
            if (rankA == rankB)
            {
                var firstDec = a.DecisionId.CompareTo(b.DecisionId) <= 0 ? a : b;
                var secondDec = firstDec == a ? b : a;

                conflicts.Add(new DetectedConflict(
                    conflictId: GenerateDeterministicConflictId("ContradictoryDecisions", subjectId, rawDecisionIds),
                    conflictType: "ContradictoryDecisions",
                    severity: "Critical",
                    detectionStatus: "Detected",
                    involvedDecisionIds: sortedDecisionIds,
                    involvedInstitutions: sortedInstitutions,
                    subjectId: subjectId,
                    explanation: $"Contradictory outcomes issued for subject {subjectId}: " +
                                 $"{firstDec.InstitutionName} issued an {firstDec.DecisionType} and {secondDec.InstitutionName} issued a {secondDec.DecisionType}.{geometryDisclaimer}",
                    evidenceRule: "Contradictory Outcomes Rule (RULE_CONTRADICTION): A single subject cannot simultaneously have both an active Approval and an active Rejection at the same authority level.",
                    recommendedAction: "Refer to Land Use Coordination Committee to resolve the conflicting status.",
                    detectionTimestamp: evaluationTimestamp
                ));
            }
        }

        // 2. Authority Level Inconsistency Check
        int rankAVal = GetAuthorityRank(a.AuthorityLevel);
        int rankBVal = GetAuthorityRank(b.AuthorityLevel);

        if (rankAVal != rankBVal)
        {
            var higher = rankAVal > rankBVal ? a : b;
            var lower = rankAVal > rankBVal ? b : a;

            string higherType = Normalize(higher.DecisionType);
            string lowerType = Normalize(lower.DecisionType);

            if (lowerType == "APPROVAL" && (higherType == "REJECTION" || higherType == "RESTRICTION"))
            {
                conflicts.Add(new DetectedConflict(
                    conflictId: GenerateDeterministicConflictId("AuthorityLevelInconsistency", subjectId, rawDecisionIds),
                    conflictType: "AuthorityLevelInconsistency",
                    severity: "Critical",
                    detectionStatus: "Detected",
                    involvedDecisionIds: sortedDecisionIds,
                    involvedInstitutions: sortedInstitutions,
                    subjectId: subjectId,
                    explanation: $"Authority level inconsistency detected on subject {subjectId}: {higher.InstitutionName} ({higher.AuthorityLevel}) issued a restrictive {higher.DecisionType} and {lower.InstitutionName} ({lower.AuthorityLevel}) issued a permissive {lower.DecisionType} during an overlapping period.{geometryDisclaimer}",
                    evidenceRule: "Authority Level Consistency Rule: Permissive decisions at a lower authority level overlap with restrictive decisions at a higher authority level.",
                    recommendedAction: "Refer to the Land Use Coordination Committee for human review of authority precedence and decision reconciliation.",
                    detectionTimestamp: evaluationTimestamp
                ));
            }
        }

        // 3. Mandate Overlap Check
        if (Normalize(a.InstitutionName) != Normalize(b.InstitutionName) &&
            !string.IsNullOrWhiteSpace(a.MandateKey) &&
            !string.IsNullOrWhiteSpace(b.MandateKey) &&
            string.Equals(a.MandateKey.Trim(), b.MandateKey.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            bool isExclusive = string.Equals(a.MandateMode.Trim(), "Exclusive", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(b.MandateMode.Trim(), "Exclusive", StringComparison.OrdinalIgnoreCase);

            if (isExclusive)
            {
                conflicts.Add(new DetectedConflict(
                    conflictId: GenerateDeterministicConflictId("MandateOverlap", subjectId, rawDecisionIds),
                    conflictType: "MandateOverlap",
                    severity: "Medium",
                    detectionStatus: "Detected",
                    involvedDecisionIds: sortedDecisionIds,
                    involvedInstitutions: sortedInstitutions,
                    subjectId: subjectId,
                    explanation: $"Overlapping exclusive mandates detected on subject {subjectId} for mandate scope '{a.MandateKey}': Both {a.InstitutionName} and {b.InstitutionName} issued separate decisions.{geometryDisclaimer}",
                    evidenceRule: "Exclusive Mandate Rule: Multiple institutions cannot exercise exclusive mandate authorities concurrently for the same subject.",
                    recommendedAction: "Request jurisdictional clarification to resolve exclusive mandate boundary conflict.",
                    detectionTimestamp: evaluationTimestamp
                ));
            }
        }

        // 4. Regulatory Reference Conflict Check
        if (!string.IsNullOrWhiteSpace(a.RegulatoryReference) && !string.IsNullOrWhiteSpace(b.RegulatoryReference))
        {
            var normRefA = Normalize(a.RegulatoryReference);
            var normRefB = Normalize(b.RegulatoryReference);

            bool aIncompatibleWithB = a.IncompatibleRegulatoryReferences != null && a.IncompatibleRegulatoryReferences
                .Select(Normalize)
                .Contains(normRefB);

            bool bIncompatibleWithA = b.IncompatibleRegulatoryReferences != null && b.IncompatibleRegulatoryReferences
                .Select(Normalize)
                .Contains(normRefA);

            if (aIncompatibleWithB || bIncompatibleWithA)
            {
                if ((typeA == "RESTRICTION" && typeB == "APPROVAL") || (typeB == "RESTRICTION" && typeA == "APPROVAL") ||
                    (typeA == "REJECTION" && typeB == "APPROVAL") || (typeB == "REJECTION" && typeA == "APPROVAL"))
                {
                    var restriction = (typeA == "RESTRICTION" || typeA == "REJECTION") ? a : b;
                    var approval = restriction == a ? b : a;

                    conflicts.Add(new DetectedConflict(
                        conflictId: GenerateDeterministicConflictId("RegulatoryConflict", subjectId, rawDecisionIds),
                        conflictType: "RegulatoryConflict",
                        severity: "High",
                        detectionStatus: "Detected",
                        involvedDecisionIds: sortedDecisionIds,
                        involvedInstitutions: sortedInstitutions,
                        subjectId: subjectId,
                        explanation: $"Regulatory conflict detected for subject {subjectId}: Approval based on '{approval.RegulatoryReference}' is incompatible with restriction based on '{restriction.RegulatoryReference}' issued by {restriction.InstitutionName}.{geometryDisclaimer}",
                        evidenceRule: "Regulatory Consistency Rule: Explicitly incompatible regulatory references cannot be co-applied with conflicting outcomes on the same land parcel.",
                        recommendedAction: "Submit the file for legal counsel review to reconcile conflicting regulatory references.",
                        detectionTimestamp: evaluationTimestamp
                    ));
                }
            }
        }

        // 5. Land-Use Incompatibility Check
        if (!string.IsNullOrWhiteSpace(a.LandUseCode) && !string.IsNullOrWhiteSpace(b.LandUseCode))
        {
            var normCodeA = Normalize(a.LandUseCode);
            var normCodeB = Normalize(b.LandUseCode);

            bool aIncompatibleWithB = a.IncompatibleLandUseCodes != null && a.IncompatibleLandUseCodes
                .Select(Normalize)
                .Contains(normCodeB);

            bool bIncompatibleWithA = b.IncompatibleLandUseCodes != null && b.IncompatibleLandUseCodes
                .Select(Normalize)
                .Contains(normCodeA);

            if (aIncompatibleWithB || bIncompatibleWithA)
            {
                var sortedLandUses = new[] { a.LandUseCode, b.LandUseCode }.OrderBy(Normalize, StringComparer.Ordinal).ToList();

                conflicts.Add(new DetectedConflict(
                    conflictId: GenerateDeterministicConflictId("LandUseIncompatibility", subjectId, rawDecisionIds),
                    conflictType: "LandUseIncompatibility",
                    severity: "High",
                    detectionStatus: "Detected",
                    involvedDecisionIds: sortedDecisionIds,
                    involvedInstitutions: sortedInstitutions,
                    subjectId: subjectId,
                    explanation: $"Land use incompatibility detected on subject {subjectId}: Land use '{sortedLandUses[0]}' is incompatible with land use '{sortedLandUses[1]}'.{geometryDisclaimer}",
                    evidenceRule: "Land Use Compatibility Rule: Explicitly incompatible land use codes cannot coexist on the same subject.",
                    recommendedAction: "Refer to zoning or municipal planning board for compatibility reconciliation.",
                    detectionTimestamp: evaluationTimestamp
                ));
            }
        }

        // 6. Duplicate Allocation / Overlapping Lease Period Check
        if (typeA == "APPROVAL" && typeB == "APPROVAL")
        {
            bool hasApplicants = !string.IsNullOrWhiteSpace(a.ApplicantId) && !string.IsNullOrWhiteSpace(b.ApplicantId);
            bool differentApplicants = hasApplicants && Normalize(a.ApplicantId) != Normalize(b.ApplicantId);
            bool sameApplicantDuplicate = hasApplicants && Normalize(a.ApplicantId) == Normalize(b.ApplicantId);
            bool activeLeaseStatus = Normalize(a.RecordStatus) == "ALLOCATED" || Normalize(b.RecordStatus) == "ALLOCATED" ||
                                     Normalize(a.RecordStatus) == "ACTIVE_LEASE" || Normalize(b.RecordStatus) == "ACTIVE_LEASE" ||
                                     Normalize(a.RecordStatus) == "LEASE_ISSUED" || Normalize(b.RecordStatus) == "LEASE_ISSUED" ||
                                     Normalize(a.RecordStatus) == "DUPLICATE_ALLOCATION" || Normalize(b.RecordStatus) == "DUPLICATE_ALLOCATION";
            bool isExplicitLeaseAllocation = (Normalize(a.ProposedUse).StartsWith("LEASE") || Normalize(b.ProposedUse).StartsWith("LEASE")) &&
                                             Normalize(a.InstitutionName) == Normalize(b.InstitutionName);

            if (differentApplicants || sameApplicantDuplicate || activeLeaseStatus || isExplicitLeaseAllocation)
            {
                string details;
                if (Normalize(a.InstitutionName) == Normalize(b.InstitutionName))
                {
                    details = $"Multiple active approvals ({a.DecisionId} and {b.DecisionId}) issued by {a.InstitutionName}";
                }
                else
                {
                    details = $"Overlapping active approvals issued by separate institutions ({a.InstitutionName} [{a.DecisionId}] and {b.InstitutionName} [{b.DecisionId}])";
                }

                string applicantInfo = string.Empty;
                if (differentApplicants)
                {
                    applicantInfo = $" Conflicting allocation to distinct parties: '{a.ApplicantId}' and '{b.ApplicantId}'.";
                }
                else if (sameApplicantDuplicate)
                {
                    applicantInfo = $" Duplicate allocation/application identified for the same applicant: '{a.ApplicantId}'.";
                }

                var explanation = $"Potential duplicate allocation detected on subject {subjectId}: {details} during an overlapping period.{applicantInfo}{geometryDisclaimer}";

                conflicts.Add(new DetectedConflict(
                    conflictId: GenerateDeterministicConflictId("DuplicateAllocation", subjectId, rawDecisionIds),
                    conflictType: "DuplicateAllocation",
                    severity: "Critical",
                    detectionStatus: "Detected",
                    involvedDecisionIds: sortedDecisionIds,
                    involvedInstitutions: sortedInstitutions,
                    subjectId: subjectId,
                    explanation: explanation,
                    evidenceRule: "Allocation Exclusivity Rule (RULE_ALLOCATION_EXCLUSIVITY): A state land parcel cannot have multiple concurrent active approvals or lease allocations during overlapping time periods without administrative reconciliation.",
                    recommendedAction: "Submit to Land Administration Review Board for administrative review to resolve duplicate allocation.",
                    detectionTimestamp: evaluationTimestamp
                ));
            }
        }

        // 7. Conflicting Lifecycle / Operational Record Status Check
        if (!string.IsNullOrWhiteSpace(a.RecordStatus) || !string.IsNullOrWhiteSpace(b.RecordStatus))
        {
            string statusA = Normalize(a.RecordStatus);
            string statusB = Normalize(b.RecordStatus);
            bool isActiveA = statusA == "ACTIVE" || statusA == "APPROVED" || statusA == "ALLOCATED" || (typeA == "APPROVAL" && string.IsNullOrEmpty(statusA));
            bool isActiveB = statusB == "ACTIVE" || statusB == "APPROVED" || statusB == "ALLOCATED" || (typeB == "APPROVAL" && string.IsNullOrEmpty(statusB));
            bool isRestrictedA = statusA == "REVOKED" || statusA == "CANCELLED" || statusA == "TERMINATED" || statusA == "RESTRICTED" || statusA == "SUSPENDED";
            bool isRestrictedB = statusB == "REVOKED" || statusB == "CANCELLED" || statusB == "TERMINATED" || statusB == "RESTRICTED" || statusB == "SUSPENDED";

            if ((isActiveA && isRestrictedB) || (isActiveB && isRestrictedA))
            {
                var active = (isActiveA && !isRestrictedA) ? a : b;
                var restricted = isRestrictedA ? a : b;

                var explanation = $"Conflicting record status detected on subject {subjectId}: Record '{active.DecisionId}' has active/permissive status while record '{restricted.DecisionId}' has restrictive status '{restricted.RecordStatus}' during an overlapping period.{geometryDisclaimer}";

                conflicts.Add(new DetectedConflict(
                    conflictId: GenerateDeterministicConflictId("ConflictingStatuses", subjectId, rawDecisionIds),
                    conflictType: "ConflictingStatuses",
                    severity: "Critical",
                    detectionStatus: "Detected",
                    involvedDecisionIds: sortedDecisionIds,
                    involvedInstitutions: sortedInstitutions,
                    subjectId: subjectId,
                    explanation: explanation,
                    evidenceRule: "Record Lifecycle Status Rule (RULE_RECORD_LIFECYCLE): An active lease or allocation record cannot coexist with a revoked, cancelled, or terminated record for the same subject during an overlapping period without administrative verification.",
                    recommendedAction: "Submit to Land Registrar for administrative verification of lease lifecycle status.",
                    detectionTimestamp: evaluationTimestamp
                ));
            }
        }
    }

    private static int GetAuthorityRank(string level)
    {
        if (string.IsNullOrWhiteSpace(level)) return 0;

        return Normalize(level) switch
        {
            "NATIONAL" => 3,
            "PROVINCIAL" => 2,
            "LOCAL" => 1,
            _ => 0
        };
    }

    private static int GetSeverityRank(string severity)
    {
        if (string.IsNullOrWhiteSpace(severity)) return 4;

        return Normalize(severity) switch
        {
            "CRITICAL" => 1,
            "HIGH" => 2,
            "MEDIUM" => 3,
            _ => 4
        };
    }
}

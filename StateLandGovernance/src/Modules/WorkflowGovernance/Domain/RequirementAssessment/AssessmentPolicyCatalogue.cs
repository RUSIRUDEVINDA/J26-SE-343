namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class AssessmentPolicyCatalogue
{
    private readonly List<AssessmentPolicy> _policies = new();

    public IReadOnlyCollection<AssessmentPolicy> Policies => _policies.AsReadOnly();

    public void RegisterPolicy(AssessmentPolicy policy)
    {
        if (policy == null)
        {
            throw new InvalidAssessmentPolicyException("Policy cannot be null.");
        }

        var exists = _policies.Any(p =>
            p.PolicyId.Equals(policy.PolicyId) &&
            string.Equals(p.Version, policy.Version, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            throw new InvalidAssessmentPolicyException($"Policy with id '{policy.PolicyId}' and version '{policy.Version}' already exists in catalogue.");
        }

        _policies.Add(policy);
    }

    public AssessmentPolicy? GetPolicy(AssessmentPolicyId id, string version)
    {
        return _policies.FirstOrDefault(p =>
            p.PolicyId.Equals(id) &&
            string.Equals(p.Version, version, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyCollection<AssessmentPolicy> GetPoliciesForSubject(AssessmentSubject subject)
    {
        return _policies.Where(p => p.Subject == subject).ToList().AsReadOnly();
    }

    public PolicyResolutionResult ResolveApplicablePolicy(
        AssessmentSubject subject,
        LeasePurpose? purpose,
        JurisdictionContext? jurisdiction,
        DateTime assessmentTime)
    {
        if (assessmentTime.Kind != DateTimeKind.Utc)
        {
            throw new InvalidAssessmentPolicyException("Assessment time must be UTC.");
        }

        // 1. Scope matching: filter policies by subject and purpose/jurisdiction compatibility
        var scopeMatching = _policies
            .Where(p => p.Subject == subject)
            .Where(p => p.ApplicablePurpose == null || (purpose.HasValue && p.ApplicablePurpose.Value.Equals(purpose.Value)))
            .Where(p => p.ApplicableJurisdiction == null || (jurisdiction.HasValue && p.ApplicableJurisdiction.Value.Equals(jurisdiction.Value)))
            .ToList();

        if (!scopeMatching.Any())
        {
            return PolicyResolutionResult.None();
        }

        static int GetSpecificity(AssessmentPolicy p) =>
            (p.ApplicablePurpose != null ? 2 : 0) + (p.ApplicableJurisdiction != null ? 1 : 0);

        // 2. Active & effective policies
        var activeEffective = scopeMatching
            .Where(p => p.Status == PolicyStatus.Active && (p.EffectivePeriod == null || p.EffectivePeriod.IsEffectiveAt(assessmentTime)))
            .ToList();

        if (activeEffective.Any())
        {
            int maxActiveScore = activeEffective.Max(GetSpecificity);
            var topActive = activeEffective
                .Where(p => GetSpecificity(p) == maxActiveScore)
                .ToList();

            if (topActive.Count == 1)
            {
                return PolicyResolutionResult.Single(topActive[0]);
            }

            // Multiple active/effective policies with equal specificity
            var sortedAmbiguous = topActive
                .OrderBy(p => p.PolicyId.Value, StringComparer.Ordinal)
                .ThenBy(p => p.Version, StringComparer.Ordinal)
                .ToList();

            return PolicyResolutionResult.Ambiguous(sortedAmbiguous);
        }

        // 3. No active & effective policy. Determine reason from candidates at highest specificity.
        int maxCandidateScore = scopeMatching.Max(GetSpecificity);
        var topCandidates = scopeMatching
            .Where(p => GetSpecificity(p) == maxCandidateScore)
            .OrderBy(p => p.PolicyId.Value, StringComparer.Ordinal)
            .ThenBy(p => p.Version, StringComparer.Ordinal)
            .ToList();

        var draft = topCandidates.FirstOrDefault(p => p.Status == PolicyStatus.Draft);
        if (draft != null)
        {
            return PolicyResolutionResult.Draft(draft);
        }

        var awaiting = topCandidates.FirstOrDefault(p => p.Status == PolicyStatus.AwaitingConfirmation);
        if (awaiting != null)
        {
            return PolicyResolutionResult.AwaitingConfirmation(awaiting);
        }

        var outOfPeriod = topCandidates.FirstOrDefault(p => p.Status == PolicyStatus.Active && p.EffectivePeriod != null && !p.EffectivePeriod.IsEffectiveAt(assessmentTime));
        if (outOfPeriod != null)
        {
            return PolicyResolutionResult.OutOfPeriod(outOfPeriod);
        }

        var inactive = topCandidates.FirstOrDefault(p => p.Status == PolicyStatus.Inactive);
        if (inactive != null)
        {
            return PolicyResolutionResult.Inactive(inactive);
        }

        return PolicyResolutionResult.None();
    }

    public AssessmentPolicy? FindApplicablePolicy(
        AssessmentSubject subject,
        LeasePurpose? purpose,
        JurisdictionContext? jurisdiction,
        DateTime assessmentTime)
    {
        var resolution = ResolveApplicablePolicy(subject, purpose, jurisdiction, assessmentTime);

        return resolution.Status switch
        {
            PolicyResolutionStatus.SingleMatch => resolution.Policy,
            PolicyResolutionStatus.AmbiguousMatch => throw new InvalidAssessmentPolicyException(
                $"Ambiguous policy configuration detected: multiple active policies with identical specificity match subject '{subject}' ({string.Join(", ", resolution.AmbiguousPolicies.Select(p => $"'{p.PolicyId}' (v{p.Version})"))})."),
            _ => null
        };
    }
}

public enum PolicyResolutionStatus
{
    NoMatch,
    SingleMatch,
    AmbiguousMatch,
    MatchingDraft,
    MatchingAwaitingConfirmation,
    MatchingInactive,
    MatchingOutOfPeriod
}

public sealed class PolicyResolutionResult
{
    public PolicyResolutionStatus Status { get; }
    public AssessmentPolicy? Policy { get; }
    public IReadOnlyList<AssessmentPolicy> MatchedPolicies { get; }
    public IReadOnlyList<AssessmentPolicy> AmbiguousPolicies =>
        Status == PolicyResolutionStatus.AmbiguousMatch ? MatchedPolicies : Array.Empty<AssessmentPolicy>();

    private PolicyResolutionResult(PolicyResolutionStatus status, AssessmentPolicy? policy, IReadOnlyList<AssessmentPolicy> matchedPolicies)
    {
        Status = status;
        Policy = policy;
        MatchedPolicies = matchedPolicies;
    }

    public static PolicyResolutionResult None() =>
        new(PolicyResolutionStatus.NoMatch, null, Array.Empty<AssessmentPolicy>());

    public static PolicyResolutionResult Single(AssessmentPolicy policy) =>
        new(PolicyResolutionStatus.SingleMatch, policy, new[] { policy });

    public static PolicyResolutionResult Ambiguous(IReadOnlyList<AssessmentPolicy> ambiguousPolicies) =>
        new(PolicyResolutionStatus.AmbiguousMatch, null, ambiguousPolicies);

    public static PolicyResolutionResult Draft(AssessmentPolicy policy) =>
        new(PolicyResolutionStatus.MatchingDraft, policy, new[] { policy });

    public static PolicyResolutionResult AwaitingConfirmation(AssessmentPolicy policy) =>
        new(PolicyResolutionStatus.MatchingAwaitingConfirmation, policy, new[] { policy });

    public static PolicyResolutionResult Inactive(AssessmentPolicy policy) =>
        new(PolicyResolutionStatus.MatchingInactive, policy, new[] { policy });

    public static PolicyResolutionResult OutOfPeriod(AssessmentPolicy policy) =>
        new(PolicyResolutionStatus.MatchingOutOfPeriod, policy, new[] { policy });
}

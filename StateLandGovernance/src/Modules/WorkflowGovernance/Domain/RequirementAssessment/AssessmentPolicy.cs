namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class AssessmentPolicy
{
    public AssessmentPolicyId PolicyId { get; }
    public string Version { get; }
    public string Name { get; }
    public string? Description { get; }
    public EffectivePeriod? EffectivePeriod { get; }
    public string AuthoritativeSourceReference { get; }
    public AssessmentSubject Subject { get; }
    public LeasePurpose? ApplicablePurpose { get; }
    public JurisdictionContext? ApplicableJurisdiction { get; }
    public decimal? ExtentThreshold { get; }
    public MeasurementUnit? ThresholdUnit { get; }
    public ComparisonOperator? ConfirmedOperator { get; }
    public bool IsEqualityBoundaryConfirmed { get; }
    public PolicyStatus Status { get; }

    public AssessmentPolicy(
        AssessmentPolicyId policyId,
        string version,
        string name,
        string authoritativeSourceReference,
        AssessmentSubject subject,
        PolicyStatus status,
        string? description = null,
        EffectivePeriod? effectivePeriod = null,
        LeasePurpose? applicablePurpose = null,
        JurisdictionContext? applicableJurisdiction = null,
        decimal? extentThreshold = null,
        MeasurementUnit? thresholdUnit = null,
        ComparisonOperator? confirmedOperator = null,
        bool isEqualityBoundaryConfirmed = true)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidAssessmentPolicyException("Policy version cannot be null, empty, or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidAssessmentPolicyException("Policy name cannot be null, empty, or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(authoritativeSourceReference))
        {
            throw new InvalidAssessmentPolicyException("Authoritative source/reference is mandatory for a policy definition.");
        }

        if (extentThreshold.HasValue)
        {
            if (extentThreshold.Value <= 0m)
            {
                throw new InvalidAssessmentPolicyException("Configurable extent threshold must be greater than zero.");
            }

            if (!thresholdUnit.HasValue || string.IsNullOrWhiteSpace(thresholdUnit.Value.Name))
            {
                throw new InvalidAssessmentPolicyException("Approved measurement unit is required when an extent threshold is defined.");
            }
        }

        PolicyId = policyId;
        Version = version.Trim();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        EffectivePeriod = effectivePeriod;
        AuthoritativeSourceReference = authoritativeSourceReference.Trim();
        Subject = subject;
        Status = status;
        ApplicablePurpose = applicablePurpose;
        ApplicableJurisdiction = applicableJurisdiction;
        ExtentThreshold = extentThreshold;
        ThresholdUnit = thresholdUnit;
        ConfirmedOperator = confirmedOperator;
        IsEqualityBoundaryConfirmed = isEqualityBoundaryConfirmed;
    }
}

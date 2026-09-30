namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public static class RequirementAssessmentEngine
{
    public static RequirementAssessmentResult EvaluateAgainstCatalogue(
        LeaseCaseId leaseCaseId,
        AssessmentSubject subject,
        LeaseProposalIntake? intake,
        AssessmentPolicyCatalogue catalogue,
        DateTime assessmentTime)
    {
        if (catalogue == null)
        {
            throw new InvalidRequirementAssessmentException("AssessmentPolicyCatalogue cannot be null.");
        }

        if (assessmentTime.Kind != DateTimeKind.Utc)
        {
            throw new InvalidRequirementAssessmentException("Assessment timestamp must be UTC.");
        }

        var resolution = catalogue.ResolveApplicablePolicy(
            subject,
            intake?.Purpose,
            intake?.Jurisdiction,
            assessmentTime);

        if (resolution.Status == PolicyResolutionStatus.AmbiguousMatch)
        {
            var policyList = string.Join(", ", resolution.AmbiguousPolicies.Select(p => $"'{p.PolicyId}' (v{p.Version})"));
            var snapshot = intake == null ? null : new AssessmentInputSnapshot(
                intake.Purpose?.Value,
                intake.RequestedExtent?.Value,
                intake.RequestedExtent?.Unit.Name,
                intake.Jurisdiction?.Code,
                intake.SourceReference.ToString());

            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                RequirementAssessmentOutcome.HumanReviewRequired,
                matchedPolicyId: null,
                matchedPolicyVersion: null,
                snapshot,
                assessmentTime,
                explanation: $"Multiple active policies match requirement subject '{subject}' with identical specificity: [{policyList}]. Human review is required.",
                sourceReference: null,
                requiresHumanConfirmation: true);
        }

        if (resolution.Status == PolicyResolutionStatus.NoMatch)
        {
            return Evaluate(leaseCaseId, subject, intake, (AssessmentPolicy?)null, assessmentTime);
        }

        return Evaluate(leaseCaseId, subject, intake, resolution.Policy!, assessmentTime);
    }

    public static RequirementAssessmentResult Evaluate(
        LeaseCaseId leaseCaseId,
        AssessmentSubject subject,
        LeaseProposalIntake? intake,
        AssessmentPolicy? policy,
        DateTime assessmentTime)
    {
        if (assessmentTime.Kind != DateTimeKind.Utc)
        {
            throw new InvalidRequirementAssessmentException("Assessment timestamp must be UTC.");
        }

        var snapshot = intake == null ? null : new AssessmentInputSnapshot(
            intake.Purpose?.Value,
            intake.RequestedExtent?.Value,
            intake.RequestedExtent?.Unit.Name,
            intake.Jurisdiction?.Code,
            intake.SourceReference.ToString());

        if (policy == null)
        {
            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                RequirementAssessmentOutcome.Undetermined,
                matchedPolicyId: null,
                matchedPolicyVersion: null,
                snapshot,
                assessmentTime,
                explanation: $"No applicable or active policy was found for requirement subject '{subject}'.",
                sourceReference: null,
                requiresHumanConfirmation: true);
        }

        if (policy.Subject != subject)
        {
            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                RequirementAssessmentOutcome.Undetermined,
                policy.PolicyId.Value,
                policy.Version,
                snapshot,
                assessmentTime,
                explanation: $"Policy '{policy.PolicyId}' subject '{policy.Subject}' does not match assessed requirement subject '{subject}'.",
                policy.AuthoritativeSourceReference,
                requiresHumanConfirmation: true);
        }

        if (policy.Status == PolicyStatus.Draft)
        {
            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                RequirementAssessmentOutcome.HumanReviewRequired,
                policy.PolicyId.Value,
                policy.Version,
                snapshot,
                assessmentTime,
                explanation: $"Policy '{policy.PolicyId}' (v{policy.Version}) is in Draft status and cannot produce an authoritative determination.",
                policy.AuthoritativeSourceReference,
                requiresHumanConfirmation: true);
        }

        if (policy.Status == PolicyStatus.AwaitingConfirmation)
        {
            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                RequirementAssessmentOutcome.HumanReviewRequired,
                policy.PolicyId.Value,
                policy.Version,
                snapshot,
                assessmentTime,
                explanation: $"Policy '{policy.PolicyId}' (v{policy.Version}) is awaiting confirmation and requires human confirmation.",
                policy.AuthoritativeSourceReference,
                requiresHumanConfirmation: true);
        }

        if (policy.Status == PolicyStatus.Inactive)
        {
            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                RequirementAssessmentOutcome.Undetermined,
                policy.PolicyId.Value,
                policy.Version,
                snapshot,
                assessmentTime,
                explanation: $"Policy '{policy.PolicyId}' (v{policy.Version}) is inactive and cannot produce an authoritative determination.",
                policy.AuthoritativeSourceReference,
                requiresHumanConfirmation: true);
        }

        if (policy.EffectivePeriod != null && !policy.EffectivePeriod.IsEffectiveAt(assessmentTime))
        {
            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                RequirementAssessmentOutcome.Undetermined,
                policy.PolicyId.Value,
                policy.Version,
                snapshot,
                assessmentTime,
                explanation: $"Policy '{policy.PolicyId}' (v{policy.Version}) is not effective at the assessment timestamp.",
                policy.AuthoritativeSourceReference,
                requiresHumanConfirmation: true);
        }

        if (intake == null)
        {
            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                RequirementAssessmentOutcome.Undetermined,
                policy.PolicyId.Value,
                policy.Version,
                snapshot,
                assessmentTime,
                explanation: "Lease case lacks required proposal intake information.",
                policy.AuthoritativeSourceReference,
                requiresHumanConfirmation: true);
        }

        if (policy.ApplicablePurpose.HasValue)
        {
            if (!intake.Purpose.HasValue)
            {
                return new RequirementAssessmentResult(
                    leaseCaseId,
                    subject,
                    RequirementAssessmentOutcome.Undetermined,
                    policy.PolicyId.Value,
                    policy.Version,
                    snapshot,
                    assessmentTime,
                    explanation: "Case is missing required lease purpose for policy evaluation.",
                    policy.AuthoritativeSourceReference,
                    requiresHumanConfirmation: true);
            }

            if (!policy.ApplicablePurpose.Value.Equals(intake.Purpose.Value))
            {
                return new RequirementAssessmentResult(
                    leaseCaseId,
                    subject,
                    RequirementAssessmentOutcome.Undetermined,
                    policy.PolicyId.Value,
                    policy.Version,
                    snapshot,
                    assessmentTime,
                    explanation: $"Policy '{policy.PolicyId}' is not applicable to case purpose '{intake.Purpose.Value}' (expected '{policy.ApplicablePurpose.Value}').",
                    policy.AuthoritativeSourceReference,
                    requiresHumanConfirmation: true);
            }
        }

        if (policy.ApplicableJurisdiction.HasValue)
        {
            if (!intake.Jurisdiction.HasValue)
            {
                return new RequirementAssessmentResult(
                    leaseCaseId,
                    subject,
                    RequirementAssessmentOutcome.Undetermined,
                    policy.PolicyId.Value,
                    policy.Version,
                    snapshot,
                    assessmentTime,
                    explanation: "Case is missing required jurisdiction context for policy evaluation.",
                    policy.AuthoritativeSourceReference,
                    requiresHumanConfirmation: true);
            }

            if (!policy.ApplicableJurisdiction.Value.Equals(intake.Jurisdiction.Value))
            {
                return new RequirementAssessmentResult(
                    leaseCaseId,
                    subject,
                    RequirementAssessmentOutcome.Undetermined,
                    policy.PolicyId.Value,
                    policy.Version,
                    snapshot,
                    assessmentTime,
                    explanation: $"Policy '{policy.PolicyId}' is not applicable to case jurisdiction '{intake.Jurisdiction.Value}' (expected '{policy.ApplicableJurisdiction.Value}').",
                    policy.AuthoritativeSourceReference,
                    requiresHumanConfirmation: true);
            }
        }

        if (policy.ExtentThreshold.HasValue)
        {
            if (!intake.RequestedExtent.HasValue)
            {
                return new RequirementAssessmentResult(
                    leaseCaseId,
                    subject,
                    RequirementAssessmentOutcome.Undetermined,
                    policy.PolicyId.Value,
                    policy.Version,
                    snapshot,
                    assessmentTime,
                    explanation: "Case is missing requested land extent required for threshold evaluation.",
                    policy.AuthoritativeSourceReference,
                    requiresHumanConfirmation: true);
            }

            var caseExtent = intake.RequestedExtent.Value;
            var policyUnit = policy.ThresholdUnit!.Value;

            if (!caseExtent.Unit.IsCompatibleWith(policyUnit))
            {
                return new RequirementAssessmentResult(
                    leaseCaseId,
                    subject,
                    RequirementAssessmentOutcome.HumanReviewRequired,
                    policy.PolicyId.Value,
                    policy.Version,
                    snapshot,
                    assessmentTime,
                    explanation: $"Incompatible measurement units: intake extent unit '{caseExtent.Unit.Name}' does not match policy threshold unit '{policyUnit.Name}'.",
                    policy.AuthoritativeSourceReference,
                    requiresHumanConfirmation: true);
            }

            decimal extentVal = caseExtent.Value;
            decimal thresholdVal = policy.ExtentThreshold.Value;

            // Unconfirmed operator protection: applies to values below, equal to, and above threshold
            if (!policy.ConfirmedOperator.HasValue || policy.ConfirmedOperator.Value == ComparisonOperator.Unconfirmed)
            {
                return new RequirementAssessmentResult(
                    leaseCaseId,
                    subject,
                    RequirementAssessmentOutcome.HumanReviewRequired,
                    policy.PolicyId.Value,
                    policy.Version,
                    snapshot,
                    assessmentTime,
                    explanation: "Comparison operator has not been confirmed; values relative to threshold cannot be authoritatively evaluated.",
                    policy.AuthoritativeSourceReference,
                    requiresHumanConfirmation: true);
            }

            // Equality boundary protection when operator is confirmed but boundary is unresolved
            if (extentVal == thresholdVal && !policy.IsEqualityBoundaryConfirmed)
            {
                return new RequirementAssessmentResult(
                    leaseCaseId,
                    subject,
                    RequirementAssessmentOutcome.HumanReviewRequired,
                    policy.PolicyId.Value,
                    policy.Version,
                    snapshot,
                    assessmentTime,
                    explanation: $"Requested extent ({extentVal} {caseExtent.Unit}) is equal to threshold ({thresholdVal} {policyUnit}), but equality boundary has not been confirmed.",
                    policy.AuthoritativeSourceReference,
                    requiresHumanConfirmation: true);
            }

            var op = policy.ConfirmedOperator.Value;
            bool isRequired;
            string compExplanation;

            switch (op)
            {
                case ComparisonOperator.GreaterThan:
                    isRequired = extentVal > thresholdVal;
                    compExplanation = isRequired
                        ? $"Requested extent ({extentVal} {caseExtent.Unit}) exceeds threshold ({thresholdVal} {policyUnit})."
                        : $"Requested extent ({extentVal} {caseExtent.Unit}) does not exceed threshold ({thresholdVal} {policyUnit}).";
                    break;

                case ComparisonOperator.GreaterThanOrEqual:
                    isRequired = extentVal >= thresholdVal;
                    compExplanation = isRequired
                        ? $"Requested extent ({extentVal} {caseExtent.Unit}) meets or exceeds threshold ({thresholdVal} {policyUnit})."
                        : $"Requested extent ({extentVal} {caseExtent.Unit}) is below threshold ({thresholdVal} {policyUnit}).";
                    break;

                case ComparisonOperator.LessThan:
                    isRequired = extentVal < thresholdVal;
                    compExplanation = isRequired
                        ? $"Requested extent ({extentVal} {caseExtent.Unit}) is less than threshold ({thresholdVal} {policyUnit})."
                        : $"Requested extent ({extentVal} {caseExtent.Unit}) is not less than threshold ({thresholdVal} {policyUnit}).";
                    break;

                case ComparisonOperator.LessThanOrEqual:
                    isRequired = extentVal <= thresholdVal;
                    compExplanation = isRequired
                        ? $"Requested extent ({extentVal} {caseExtent.Unit}) is less than or equal to threshold ({thresholdVal} {policyUnit})."
                        : $"Requested extent ({extentVal} {caseExtent.Unit}) exceeds threshold ({thresholdVal} {policyUnit}).";
                    break;

                case ComparisonOperator.ExactMatch:
                    isRequired = extentVal == thresholdVal;
                    compExplanation = isRequired
                        ? $"Requested extent ({extentVal} {caseExtent.Unit}) exactly matches threshold ({thresholdVal} {policyUnit})."
                        : $"Requested extent ({extentVal} {caseExtent.Unit}) does not match threshold ({thresholdVal} {policyUnit}).";
                    break;

                default:
                    return new RequirementAssessmentResult(
                        leaseCaseId,
                        subject,
                        RequirementAssessmentOutcome.HumanReviewRequired,
                        policy.PolicyId.Value,
                        policy.Version,
                        snapshot,
                        assessmentTime,
                        explanation: $"Unrecognized comparison operator '{op}'.",
                        policy.AuthoritativeSourceReference,
                        requiresHumanConfirmation: true);
            }

            var outcome = isRequired ? RequirementAssessmentOutcome.Required : RequirementAssessmentOutcome.NotRequired;
            return new RequirementAssessmentResult(
                leaseCaseId,
                subject,
                outcome,
                policy.PolicyId.Value,
                policy.Version,
                snapshot,
                assessmentTime,
                compExplanation,
                policy.AuthoritativeSourceReference,
                requiresHumanConfirmation: false);
        }

        return new RequirementAssessmentResult(
            leaseCaseId,
            subject,
            RequirementAssessmentOutcome.Required,
            policy.PolicyId.Value,
            policy.Version,
            snapshot,
            assessmentTime,
            explanation: $"Requirement '{subject}' is required based on confirmed policy '{policy.PolicyId}' (v{policy.Version}).",
            policy.AuthoritativeSourceReference,
            requiresHumanConfirmation: false);
    }
}

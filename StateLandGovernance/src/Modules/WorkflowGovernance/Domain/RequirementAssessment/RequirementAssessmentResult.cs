namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class RequirementAssessmentResult
{
    public AssessmentResultId Id { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public AssessmentSubject Subject { get; }
    public RequirementAssessmentOutcome Outcome { get; }
    public string? MatchedPolicyId { get; }
    public string? MatchedPolicyVersion { get; }
    public AssessmentInputSnapshot? EvaluatedInputs { get; }
    public DateTime AssessedAt { get; }
    public string Explanation { get; }
    public string? SourceReference { get; }
    public bool RequiresHumanConfirmation { get; }

    public RequirementAssessmentResult(
        LeaseCaseId leaseCaseId,
        AssessmentSubject subject,
        RequirementAssessmentOutcome outcome,
        string? matchedPolicyId,
        string? matchedPolicyVersion,
        AssessmentInputSnapshot? evaluatedInputs,
        DateTime assessedAt,
        string explanation,
        string? sourceReference,
        bool requiresHumanConfirmation)
        : this(
            AssessmentResultId.New(),
            leaseCaseId,
            subject,
            outcome,
            matchedPolicyId,
            matchedPolicyVersion,
            evaluatedInputs,
            assessedAt,
            explanation,
            sourceReference,
            requiresHumanConfirmation)
    {
    }

    public RequirementAssessmentResult(
        AssessmentResultId id,
        LeaseCaseId leaseCaseId,
        AssessmentSubject subject,
        RequirementAssessmentOutcome outcome,
        string? matchedPolicyId,
        string? matchedPolicyVersion,
        AssessmentInputSnapshot? evaluatedInputs,
        DateTime assessedAt,
        string explanation,
        string? sourceReference,
        bool requiresHumanConfirmation)
    {
        if (id.Value == Guid.Empty)
        {
            throw new InvalidRequirementAssessmentException("AssessmentResultId cannot be empty.");
        }

        if (assessedAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidRequirementAssessmentException("AssessedAt timestamp must be UTC.");
        }

        if (string.IsNullOrWhiteSpace(explanation))
        {
            throw new InvalidRequirementAssessmentException("Assessment explanation cannot be null, empty, or whitespace.");
        }

        Id = id;
        LeaseCaseId = leaseCaseId;
        Subject = subject;
        Outcome = outcome;
        MatchedPolicyId = matchedPolicyId;
        MatchedPolicyVersion = matchedPolicyVersion;
        EvaluatedInputs = evaluatedInputs;
        AssessedAt = assessedAt;
        Explanation = explanation;
        SourceReference = sourceReference;
        RequiresHumanConfirmation = requiresHumanConfirmation;
    }
}

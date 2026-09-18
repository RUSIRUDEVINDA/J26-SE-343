namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.Events;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;
using StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment.Events;

public sealed class LeaseCase
{
    public LeaseCaseId Id { get; }
    public string ApplicationReference { get; }
    public Guid CreatedByActorId { get; }
    public DateTime CreatedAt { get; }
    public int Revision { get; private set; }
    public LeaseProposalIntake? ProposalIntake { get; private set; }

    private readonly List<RequirementAssessmentResult> _assessmentHistory = new();
    public IReadOnlyCollection<RequirementAssessmentResult> AssessmentHistory => _assessmentHistory.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public LeaseCase(
        LeaseCaseId id,
        string applicationReference,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot)
        : this(id, applicationReference, actorId, actionTime, authoritySnapshot, null)
    {
    }

    public LeaseCase(
        LeaseCaseId id,
        string applicationReference,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot,
        LeaseProposalIntake? proposalIntake)
    {
        if (id == default || id.Value == Guid.Empty)
        {
            throw new InvalidLeaseCaseException("LeaseCaseId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(applicationReference))
        {
            throw new InvalidLeaseCaseException("ApplicationReference cannot be null, empty, or whitespace.");
        }

        if (actorId == Guid.Empty)
        {
            throw new InvalidLeaseCaseException("ActorId cannot be empty.");
        }

        if (authoritySnapshot == null)
        {
            throw new InvalidLeaseCaseException("VerifiedAuthoritySnapshot is required.");
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, id.Value.ToString());
        
        authoritySnapshot.EnsureAuthorizes(actorId, "LeaseInitiator", requiredScope, actionTime);

        Id = id;
        ApplicationReference = applicationReference;
        CreatedByActorId = actorId;
        CreatedAt = actionTime;
        ProposalIntake = proposalIntake;
        Revision = 1;

        _domainEvents.Add(new LeaseCaseInitialized(
            Guid.NewGuid(),
            actionTime,
            id,
            applicationReference,
            actorId,
            Revision
        ));
    }

    public void RecordProposalIntake(
        LeaseProposalIntake intake,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (intake == null)
        {
            throw new InvalidProposalIntakeException("LeaseProposalIntake cannot be null.");
        }

        if (authoritySnapshot == null)
        {
            throw new MissingVerifiedAuthorityException("VerifiedAuthoritySnapshot is required.");
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, Id.Value.ToString());
        authoritySnapshot.EnsureAuthorizes(actorId, "LeaseInitiator", requiredScope, actionTime);

        ProposalIntake = intake;
        Revision++;
    }

    public void RecordAssessmentResult(RequirementAssessmentResult result)
    {
        if (result == null)
        {
            throw new InvalidRequirementAssessmentException("RequirementAssessmentResult cannot be null.");
        }

        if (!result.LeaseCaseId.Equals(Id))
        {
            throw new InvalidRequirementAssessmentException("Assessment result LeaseCaseId does not match this lease case.");
        }

        if (_assessmentHistory.Any(a => a.Id.Equals(result.Id)))
        {
            throw new InvalidRequirementAssessmentException($"Assessment result with ID '{result.Id}' has already been recorded for this lease case.");
        }

        var previous = _assessmentHistory.LastOrDefault(a => a.Subject == result.Subject);
        _assessmentHistory.Add(result);

        if (previous != null)
        {
            _domainEvents.Add(new RequirementReassessed(
                Guid.NewGuid(),
                result.AssessedAt,
                Id,
                result.Subject,
                result.Outcome,
                previous.MatchedPolicyVersion,
                result.MatchedPolicyVersion
            ));
        }
        else
        {
            if (result.RequiresHumanConfirmation)
            {
                _domainEvents.Add(new RequirementAssessmentHumanReviewRequired(
                    Guid.NewGuid(),
                    result.AssessedAt,
                    Id,
                    result.Subject,
                    result.Explanation,
                    result.MatchedPolicyId,
                    result.MatchedPolicyVersion
                ));
            }
            else
            {
                _domainEvents.Add(new RequirementAssessmentCompleted(
                    Guid.NewGuid(),
                    result.AssessedAt,
                    Id,
                    result.Subject,
                    result.Outcome,
                    result.MatchedPolicyId,
                    result.MatchedPolicyVersion,
                    result.RequiresHumanConfirmation
                ));
            }
        }
    }

    public RequirementAssessmentResult? GetLatestAssessment(AssessmentSubject subject)
    {
        return _assessmentHistory.LastOrDefault(a => a.Subject == subject);
    }
}

using System;
using System.Collections.Generic;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Events;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.LeaseFeasibility.Domain.Entities;

public sealed class FinancialFeasibilityAssessment : Entity
{
    private readonly List<FinancialDocumentEvidence> _evidence = new();

    public string ApplicationId { get; private set; }
    public FeasibilityGrade Grade { get; private set; }
    public FeasibilityScoreBreakdown ScoreBreakdown { get; private set; }
    public ApprovalProbability? PredictiveProbability { get; private set; }
    public bool IsFinalized { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }

    public IReadOnlyCollection<FinancialDocumentEvidence> Evidence => _evidence.AsReadOnly();

    private FinancialFeasibilityAssessment()
    {
    }

    public FinancialFeasibilityAssessment(
        string applicationId,
        FeasibilityGrade grade,
        FeasibilityScoreBreakdown scoreBreakdown,
        ApprovalProbability? predictiveProbability = null)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new ArgumentException("Application ID is required.", nameof(applicationId));
        }

        if (scoreBreakdown == null)
        {
            throw new ArgumentNullException(nameof(scoreBreakdown));
        }

        ApplicationId = applicationId.Trim();
        Grade = grade;
        ScoreBreakdown = scoreBreakdown;
        PredictiveProbability = predictiveProbability;
        IsFinalized = false;
        GeneratedAt = DateTimeOffset.UtcNow;
    }

    public void FinalizeAssessment()
    {
        if (IsFinalized) return;
        IsFinalized = true;
        AddDomainEvent(new FinancialFeasibilityAssessmentFinalizedEvent(Id, ApplicationId, Grade));
    }

    public void AddEvidence(FinancialDocumentEvidence evidenceItem)
    {
        if (evidenceItem == null) throw new ArgumentNullException(nameof(evidenceItem));
        _evidence.Add(evidenceItem);
    }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

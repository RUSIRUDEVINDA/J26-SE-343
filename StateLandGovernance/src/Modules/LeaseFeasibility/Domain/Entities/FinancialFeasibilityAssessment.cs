using System;
using System.Collections.Generic;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Events;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.LeaseFeasibility.Domain.Entities;

public sealed class FinancialFeasibilityAssessment : Entity
{
    private readonly List<FinancialDocumentEvidence> _evidence = new();

    public string ApplicationId { get; private set; } = null!;
    public string ApplicantId { get; private set; } = null!;
    public string ContractVersion { get; private set; } = null!;
    public FeasibilityGrade Grade { get; private set; }
    public FeasibilityAction Action { get; private set; }
    public FeasibilityScoreBreakdown ScoreBreakdown { get; private set; } = null!;
    public ApprovalProbability? PredictiveProbability { get; private set; }
    public bool IsFinalized { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }

    public IReadOnlyCollection<FinancialDocumentEvidence> Evidence => _evidence.AsReadOnly();

    private FinancialFeasibilityAssessment()
    {
    }

    public FinancialFeasibilityAssessment(
        string applicationId,
        string applicantId,
        string contractVersion,
        FeasibilityGrade grade,
        FeasibilityAction action,
        FeasibilityScoreBreakdown scoreBreakdown,
        DateTimeOffset generatedAt,
        ApprovalProbability? predictiveProbability = null)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new ArgumentException("Application ID is required.", nameof(applicationId));
        }

        if (string.IsNullOrWhiteSpace(applicantId))
        {
            throw new ArgumentException("Applicant ID is required.", nameof(applicantId));
        }

        if (string.IsNullOrWhiteSpace(contractVersion))
        {
            throw new ArgumentException("Scoring contract version is required.", nameof(contractVersion));
        }

        if (scoreBreakdown == null)
        {
            throw new ArgumentNullException(nameof(scoreBreakdown));
        }

        ApplicationId = applicationId.Trim();
        ApplicantId = applicantId.Trim();
        ContractVersion = contractVersion.Trim();
        Grade = grade;
        Action = action;
        ScoreBreakdown = scoreBreakdown;
        PredictiveProbability = predictiveProbability;
        IsFinalized = false;
        GeneratedAt = generatedAt.ToUniversalTime();
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

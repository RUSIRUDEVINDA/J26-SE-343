using System;
using System.Collections.Generic;
using StateLandGovernance.LeaseFeasibility.Domain.Events;

namespace StateLandGovernance.LeaseFeasibility.Domain.Entities;

public sealed class LeaseProposal : Entity
{
    private readonly List<string> _referencedHistoricalCaseIds = new();
    private readonly List<string> _specialConditions = new();

    public string ApplicationId { get; private set; }
    public decimal RecommendedAreaHectares { get; private set; }
    public int RecommendedDurationYears { get; private set; }
    public decimal EstimatedMonthlyRent { get; private set; }
    public decimal OptimizationConfidence { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }
    public bool IsFinalized { get; private set; }

    public IReadOnlyCollection<string> ReferencedHistoricalCaseIds => _referencedHistoricalCaseIds.AsReadOnly();
    public IReadOnlyCollection<string> SpecialConditions => _specialConditions.AsReadOnly();

    private LeaseProposal()
    {
    }

    public LeaseProposal(
        string applicationId,
        decimal recommendedAreaHectares,
        int recommendedDurationYears,
        decimal estimatedMonthlyRent,
        decimal optimizationConfidence)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new ArgumentException("Application ID is required.", nameof(applicationId));
        }

        if (recommendedAreaHectares <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recommendedAreaHectares), "Recommended area must be greater than zero.");
        }

        if (recommendedDurationYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recommendedDurationYears), "Recommended duration must be greater than zero.");
        }

        if (estimatedMonthlyRent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedMonthlyRent), "Estimated monthly rent cannot be negative.");
        }

        if (optimizationConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(optimizationConfidence), "Optimization confidence must be between 0 and 1.");
        }

        ApplicationId = applicationId.Trim();
        RecommendedAreaHectares = recommendedAreaHectares;
        RecommendedDurationYears = recommendedDurationYears;
        EstimatedMonthlyRent = estimatedMonthlyRent;
        OptimizationConfidence = optimizationConfidence;
        GeneratedAt = DateTimeOffset.UtcNow;
        IsFinalized = false;
    }

    public void AddReferencedCase(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId)) throw new ArgumentException("Case ID is required.", nameof(caseId));
        _referencedHistoricalCaseIds.Add(caseId.Trim());
    }

    public void AddSpecialCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) throw new ArgumentException("Condition is required.", nameof(condition));
        _specialConditions.Add(condition.Trim());
    }

    public void FinalizeProposal()
    {
        if (IsFinalized) return;
        IsFinalized = true;
        AddDomainEvent(new LeaseProposalGeneratedEvent(Id, ApplicationId, OptimizationConfidence));
    }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

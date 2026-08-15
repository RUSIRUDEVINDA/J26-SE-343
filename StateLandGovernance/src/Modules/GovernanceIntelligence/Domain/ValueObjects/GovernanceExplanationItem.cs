using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a single structured explanation item.
/// </summary>
public sealed record GovernanceExplanationItem
{
    public string ItemId { get; }
    public EngineType SourceEngine { get; }
    public string OutcomeStatus { get; }
    public GovernanceExplanationSeverity Severity { get; }
    public string ReasonCode { get; }
    public string Title { get; }
    public string PlainLanguageExplanation { get; }
    public IReadOnlyList<string> EvidenceSummaries { get; }
    public string RecommendedAction { get; }
    public bool RequiresHumanAttention { get; }

    public GovernanceExplanationItem(
        string itemId,
        EngineType sourceEngine,
        string outcomeStatus,
        GovernanceExplanationSeverity severity,
        string reasonCode,
        string title,
        string plainLanguageExplanation,
        IEnumerable<string>? evidenceSummaries,
        string recommendedAction,
        bool requiresHumanAttention)
    {
        ItemId = itemId ?? string.Empty;
        SourceEngine = sourceEngine;
        OutcomeStatus = outcomeStatus ?? string.Empty;
        Severity = severity;
        ReasonCode = reasonCode ?? string.Empty;
        Title = title ?? string.Empty;
        PlainLanguageExplanation = plainLanguageExplanation ?? string.Empty;
        EvidenceSummaries = (evidenceSummaries ?? Array.Empty<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
        RecommendedAction = recommendedAction ?? string.Empty;
        RequiresHumanAttention = requiresHumanAttention;
    }
}

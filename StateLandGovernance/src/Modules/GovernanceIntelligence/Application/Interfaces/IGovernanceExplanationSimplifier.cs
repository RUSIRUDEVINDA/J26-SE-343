using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

public sealed record GovernanceExplanationSimplificationItem(
    string ItemId,
    string Title,
    string OutcomeStatus,
    string Severity,
    string ReasonCode,
    IReadOnlyList<string> EvidenceSummaries,
    string RecommendedAction);

public sealed record GovernanceExplanationSimplificationRequest(
    IReadOnlyList<GovernanceExplanationSimplificationItem> Items);

public sealed record GovernanceExplanationSimplificationResult(
    bool Applied,
    string Provider,
    string Model,
    string PromptVersion,
    string Status,
    string? OutputHash,
    IReadOnlyDictionary<string, string> PlainLanguageExplanations)
{
    public static GovernanceExplanationSimplificationResult NotApplied(
        string status,
        string provider = "Deterministic",
        string model = "None",
        string promptVersion = "deterministic-v1") =>
        new(false, provider, model, promptVersion, status, null, new Dictionary<string, string>());
}

/// <summary>
/// Optionally rewrites deterministic governance findings into simpler language.
/// Implementations must never change statuses, evidence, rule codes, or recommended actions.
/// </summary>
public interface IGovernanceExplanationSimplifier
{
    Task<GovernanceExplanationSimplificationResult> SimplifyAsync(
        GovernanceExplanationSimplificationRequest request,
        CancellationToken cancellationToken = default);
}

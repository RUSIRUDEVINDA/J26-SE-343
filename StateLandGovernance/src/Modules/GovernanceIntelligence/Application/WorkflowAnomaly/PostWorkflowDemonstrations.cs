using System.Globalization;
using StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;

namespace StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

public sealed class PostWorkflowDemonstrationOptions
{
    public bool Enabled { get; set; }
    public bool WritesEnabled { get; set; }
}

// These are C4-owned fictional histories, never C3 executions or evidence of legal completion.
public static class PostWorkflowDemonstrations
{
    public const string Version = "c4-fictional-workflow-v1";
    public const string CasePrefix = "C4-FICTIONAL-";
    public static readonly string[] Scenarios = ["administrative", "delays-and-revisions", KiwulaCatalogue.WorkflowScenario];
    public static string VersionFor(string scenario) => scenario == KiwulaCatalogue.WorkflowScenario ? "kiwula-completed-example/1" : Version;
    public static bool IsDemonstration(string caseId) => caseId.StartsWith(CasePrefix, StringComparison.Ordinal);

    public static Component3WorkflowGovernanceProjection Create(string scenario, Guid run, Guid projectionId, DateTimeOffset created)
    {
        if (!Scenarios.Contains(scenario, StringComparer.Ordinal)) throw new ArgumentException("Unknown server demonstration.");
        var steps = new List<(string Activity, string Institution, int Day)>
        {
            ("Application Received", "DS", 0), ("Document Review", "DS", 2),
            ("Site Inspection", "LAND", 5), ("Recommendation", "DS", 8)
        };
        if (scenario == "delays-and-revisions") steps.AddRange([
            ("Document Review", "DS", 45), ("Recommendation", "DS", 55),
            ("Decision Revision", "COMMISSIONER", 90), ("Recommendation", "DS", 120)]);
        if (scenario == KiwulaCatalogue.WorkflowScenario) steps = [
            ("Application Received", "EXAMPLE-DS", 0), ("Document Review", "EXAMPLE-LAND-TECHNICAL", 3),
            ("Recommendation", "EXAMPLE-AGRICULTURE", 6), ("DS Review", "EXAMPLE-DS", 9),
            ("Regional Review", "EXAMPLE-REGIONAL-UNCONFIRMED", 12)];
        var version = VersionFor(scenario);
        var last = steps[^1].Day;
        steps.Add(("Commissioner Decision", "COMMISSIONER", last + 3));
        steps.Add(("DS Final Notification", "DS", last + 5));
        var start = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.FromMinutes(330));
        var events = steps.Select((s, i) => new Component3WorkflowProjectionEvent(
            $"fictional-{i + 1}", i + 1, s.Activity, $"DEMO-{i + 1}", "Fictional role", s.Institution,
            start.AddDays(s.Day), "Fictional demonstration event", $"fictional://{version}/{scenario}/{i + 1}",
            $"fictional-{s.Institution}", start.AddDays(s.Day).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture))).ToArray();
        var caseId = CasePrefix + run.ToString("D");
        var provenance = GovernanceInputProvenance.CreatePp1Fixture("C4 server fictional workflow catalogue", scenario,
            version, created, scenario, created);
        var missing = new TimingBoundaryEvidence(null, TimingBoundaryEvidenceStatus.Unavailable,
            "Fictional demonstration; no legal approval boundary asserted", scenario, caseId, run);
        var hash = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(run, caseId, "Fictional PP1",
            Component3WorkflowTraceStatus.Completed, Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "DS-FINAL-NOTIFICATION", Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo", events, missing, missing, provenance);
        return new(projectionId, run, caseId, "Fictional PP1", Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring, "DS-FINAL-NOTIFICATION",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset, "Asia/Colombo", events, missing, missing,
            hash, provenance, created);
    }
}

public sealed record PostWorkflowAttempt(int Number, DateTimeOffset FinishedAtUtc, string State, string? Failure);
public sealed record PostWorkflowReport(Guid Id, Guid CreatorActorId, string Scenario, string Version,
    DateTimeOffset CreatedAtUtc, string State, string? Failure, Component3WorkflowGovernanceProjection Projection,
    WorkflowAnomalyAssessment? Assessment, int Attempt, IReadOnlyList<PostWorkflowAttempt> Attempts);

public interface IPostWorkflowDemonstrationStore
{
    Task<IReadOnlyList<PostWorkflowReport>> ListAsync(Guid actor, CancellationToken ct);
    Task<PostWorkflowReport> GetAsync(Guid actor, Guid id, CancellationToken ct);
    Task<PostWorkflowReport> RunAsync(Guid actor, string scenario, bool retry, CancellationToken ct, int expectedAttempt = 0);
}

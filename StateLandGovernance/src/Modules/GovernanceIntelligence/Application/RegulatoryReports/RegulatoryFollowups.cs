using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;

public sealed record RecordRegulatoryFollowup(Guid SubmissionKey, string RuleCode, string Operation,
    string? ReviewOutcome, string? MissingInformation, string? Remarks = null, string[]? References = null, int ExpectedRevision = 0);
public sealed record RegulatoryFollowupEvent(Guid Id, Guid EvaluationId, string RuleCode, string RuleVersion,
    Guid ActorId, DateTimeOffset CreatedAtUtc, string Operation, string? ReviewOutcome, string? MissingInformation,
    string Remarks, string[] References, string Status, int Revision, Guid? PreviousEventId, string EventSha256);
public sealed record RegulatoryFollowupHistory(bool CanWrite, IReadOnlyList<RegulatoryFollowupEvent> History,
    IReadOnlyList<FindingClosureEligibility>? ClosureEligibility = null);
public sealed record RegulatoryFollowupSaved(RegulatoryFollowupEvent Event, int CurrentRevision, bool Reused);
public sealed record PreviousRegulatoryFollowup(Guid EvaluationId, DateTime EvaluationTimestamp, RegulatoryFollowupEvent Event);
public sealed record PreviousRegulatoryFollowupPage(IReadOnlyList<PreviousRegulatoryFollowup> Items, string? NextCursor);
public sealed record RegulatoryFollowupCursor(DateTimeOffset Timestamp, Guid EventId)
{
    public string Encode() => Timestamp.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + EventId.ToString("N");
    public static RegulatoryFollowupCursor? Parse(string? value)
    {
        if (value == null) return null;
        var parts = value.Split('.');
        if (value.Length > 64 || parts.Length != 2 || !long.TryParse(parts[0], System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var ticks) || ticks < 0 || ticks > DateTimeOffset.MaxValue.Ticks
            || !Guid.TryParseExact(parts[1], "N", out var id) || id == Guid.Empty)
            throw new ArgumentException("Invalid history cursor.");
        return new(new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }
}
public interface IRegulatoryFollowupStore
{
    Task<RegulatoryFollowupHistory> ReadAsync(Guid evaluation, CancellationToken ct);
    Task<PreviousRegulatoryFollowupPage> PreviousAsync(Guid evaluation, string? cursor, int limit, CancellationToken ct);
    Task<RegulatoryFollowupSaved> RecordAsync(Guid evaluation, RecordRegulatoryFollowup input, CancellationToken ct);
}
public sealed class RegulatoryFollowupConflictException : Exception;
public sealed class RegulatoryFollowupUnavailableException : Exception;

public static class RegulatoryFollowupInput
{
    public static readonly string[] Outcomes = ["NoAdditionalConcern", "AdditionalInformationRequired", "FurtherInstitutionalLegalReview"];
    public static RecordRegulatoryFollowup Validate(RecordRegulatoryFollowup value)
    {
        if (value.SubmissionKey == Guid.Empty || value.ExpectedRevision < 0 || string.IsNullOrWhiteSpace(value.RuleCode)
            || value.RuleCode.Length > 100 || value.References?.Length > 10 || value.Operation != FindingClosurePolicy.Operation && value.References == null)
            throw new ArgumentException("Supply a submission key, saved rule code, nonnegative revision and at most ten references.");
        static string Text(string? text) => text?.Trim() ?? "";
        var remarks = Text(value.Remarks);
        if (remarks.Length > 2000 || value.Operation != FindingClosurePolicy.Operation && remarks.Length < 20) throw new ArgumentException("Remarks exceed bounds for the selected action.");
        var references = (value.References ?? []).Select(Text).ToArray();
        if (references.Any(x => x.Length is < 1 or > 500)) throw new ArgumentException("Each reference must contain1–500characters.");
        string? missing = value.MissingInformation?.Trim();
        if (value.Operation == "ClarificationRequested")
        {
            if (value.ReviewOutcome != null || missing == null || missing.Length is < 20 or > 2000)
                throw new ArgumentException("Clarification requires20–2000characters describing missing information and no review outcome.");
        }
        else if (value.Operation == "HumanReviewRecorded")
        {
            if (value.MissingInformation != null || !Outcomes.Contains(value.ReviewOutcome))
                throw new ArgumentException("Select a supported human-review outcome; missing-information text is only for clarification.");
        }
        else if (value.Operation == "NoFollowupRequired")
        {
            if (value.ReviewOutcome != null || value.MissingInformation != null)
                throw new ArgumentException("No Follow-up Required accepts a rationale only; review outcome and missing information must be null.");
        }
        else if (value.Operation == FindingClosurePolicy.Operation)
            FindingClosurePolicy.ValidatePayload(value with { Remarks = remarks, References = references });
        else throw new ArgumentException("Unsupported follow-up operation.");
        return value with { RuleCode = value.RuleCode.Trim(), Remarks = remarks, MissingInformation = missing, References = references };
    }
    public static void ValidateFinding(RecordRegulatoryFollowup input, ComplianceFindingDto finding)
    {
        if (input.Operation == FindingClosurePolicy.Operation)
        {
            var eligibility = FindingClosurePolicy.Evaluate(finding, []);
            if (!eligibility.CanClose) throw new ArgumentException(eligibility.Reason);
        }
        // This is an administrative follow-up permission over the immutable saved
        // outcome, not a new rule evaluation. Unknown/conditional/adverse/missing
        // outcomes fail closed; no earlier human note can override saved flags.
        if (input.Operation == "NoFollowupRequired" && !CanRecordNoFollowup(finding))
            throw new ArgumentException("No Follow-up Required is permitted only for a saved Compliant finding with no blocking or mandatory human-review requirement. Missing information, conditional outcomes and required institutional/legal review cannot be waived.");
        if (input.Operation == "ClarificationRequested" && finding.Status != "InsufficientInformation"
            || input.Operation == "HumanReviewRecorded" && !finding.RequiresHumanReview && finding.Status is not ("RequiresHumanReview" or "RequiresReview"))
            throw new ArgumentException("This action does not match the original saved finding.");
    }
    public static bool CanRecordNoFollowup(ComplianceFindingDto finding) =>
        finding.Status == "Compliant" && !finding.IsBlocking && !finding.RequiresHumanReview;
    public static string FindingHash(ComplianceFindingDto finding) => RegulatoryJson.Hash(JsonSerializer.Serialize(finding, RegulatoryJson.Options));
}

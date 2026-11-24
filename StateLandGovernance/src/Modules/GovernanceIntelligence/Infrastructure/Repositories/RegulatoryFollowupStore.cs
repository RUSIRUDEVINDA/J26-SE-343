using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public sealed class RegulatoryFollowupStore(GovernanceIntelligenceDbContext db, IComplaintActorContext actor,
    IComplaintOfficerDirectory directory, IndependentRegulatoryReportQueries reports, IConfiguration config) : IRegulatoryFollowupStore
{
    DbSet<RegulatoryFollowupEntity> Rows => db.Set<RegulatoryFollowupEntity>();
    async Task<(IndependentRegulatoryReport Report, bool CanWrite)> Scope(Guid evaluation, bool write, CancellationToken ct)
    {
        if (actor.ActorId == Guid.Empty) throw new UnauthorizedAccessException();
        var report = await reports.GetAsync(actor.ActorId, evaluation, ct) ?? throw new KeyNotFoundException();
        bool eligible = (await directory.ReadAsync(ct)).Officers.Any(x => x.ActorId == actor.ActorId);
        // Read assignments and global Commissioner read authority never grant writes.
        bool owner = eligible && await db.Set<RegulatoryEvaluationProvenanceEntity>().AsNoTracking()
            .AnyAsync(x => x.EvaluationId == evaluation && x.CreatorActorId == actor.ActorId, ct);
        bool enabled = config.GetValue<bool>("GovernanceIntelligence:RegulatoryProvenance:Enabled")
            && config.GetValue<bool>("GovernanceIntelligence:RegulatoryProvenance:WritesEnabled");
        if (write && !owner) throw new UnauthorizedAccessException();
        if (write && !enabled) throw new RegulatoryFollowupUnavailableException();
        return (report, owner && enabled);
    }
    static ComplianceFindingDto Finding(IndependentRegulatoryReport report, string code)
    {
        var findings = report.Findings?.Where(x => x.RuleCode == code).ToArray();
        if (findings?.Length != 1 || string.IsNullOrWhiteSpace(findings[0].RuleVersion) || findings[0].RuleVersion.Length > 100)
            throw new ArgumentException("Select one unambiguous finding from the saved assessment.");
        return findings[0];
    }
    static RegulatoryFollowupEvent View(RegulatoryFollowupEntity row, IndependentRegulatoryReport report)
    {
        var finding = Finding(report, row.RuleCode);
        if (finding.RuleVersion != row.RuleVersion || row.Canonical(RegulatoryFollowupInput.FindingHash(finding)) != row.CanonicalJson
            || RegulatoryJson.Hash(row.CanonicalJson) != row.EventSha256) throw new RegulatoryFollowupUnavailableException();
        return new(row.Id, row.EvaluationId, row.RuleCode, row.RuleVersion, row.ActorId, row.CreatedAtUtc, row.Operation,
            row.ReviewOutcome, row.MissingInformation, row.Remarks, JsonSerializer.Deserialize<string[]>(row.ReferencesJson)!,
            row.Status, row.Revision, row.PreviousEventId, row.EventSha256);
    }
    public async Task<RegulatoryFollowupHistory> ReadAsync(Guid evaluation, CancellationToken ct)
    {
        var scope = await Scope(evaluation, false, ct);
        var rows = await Rows.AsNoTracking().Where(x => x.EvaluationId == evaluation).OrderBy(x => x.RuleCode)
            .ThenBy(x => x.Revision).Take(1000).ToArrayAsync(ct);
        var history = rows.Select(x => View(x, scope.Report)).ToArray();
        scope = await Scope(evaluation, false, ct);
        return new(scope.CanWrite, history, scope.Report.Findings?.Select(f => FindingClosurePolicy.Evaluate(f, history)).ToArray());
    }
    public async Task<PreviousRegulatoryFollowupPage> PreviousAsync(Guid evaluation, string? cursor, int limit, CancellationToken ct)
    {
        var anchor = await Scope(evaluation, false, ct);
        if (limit is < 1 or > 50) throw new ArgumentException("History limit must be1–50.");
        var position = RegulatoryFollowupCursor.Parse(cursor);
        // The proposal identity comes only from the authorized saved assessment, never a browser label or query parameter.
        var proposal = anchor.Report.Metadata.ProposalId;
        if (string.IsNullOrWhiteSpace(proposal)) return new([], null);
        var allowed = await reports.ReadableIdsAsync(actor.ActorId, ct);
        var query = from row in Rows.AsNoTracking()
                    join assessment in db.ComplianceEvaluations.AsNoTracking() on row.EvaluationId equals assessment.Id
                    where assessment.Id != evaluation && assessment.ProposalId == proposal && allowed.Contains(assessment.Id)
                    select new { Row = row, assessment.EvaluationTimestamp };
        if (position != null)
            query = query.Where(x => x.Row.CreatedAtUtc < position.Timestamp ||
                x.Row.CreatedAtUtc == position.Timestamp && x.Row.Id.CompareTo(position.EventId) < 0);
        // Scope is applied before keyset pagination; no first100-global-records filtering or unbounded event payload.
        var page = await query.OrderByDescending(x => x.Row.CreatedAtUtc).ThenByDescending(x => x.Row.Id).Take(limit + 1).ToArrayAsync(ct);
        var selected = page.Take(limit).ToArray();
        var originals = new Dictionary<Guid, IndependentRegulatoryReport>();
        foreach (var id in selected.Select(x => x.Row.EvaluationId).Distinct())
        {
            var report = await reports.GetAsync(actor.ActorId, id, ct) ?? throw new KeyNotFoundException();
            if (report.Metadata.ProposalId != proposal) throw new UnauthorizedAccessException();
            originals[id] = report;
        }
        var items = selected.Select(x => new PreviousRegulatoryFollowup(x.Row.EvaluationId, x.EvaluationTimestamp,
            View(x.Row, originals[x.Row.EvaluationId]))).ToArray();
        foreach (var id in originals.Keys) await reports.GetAsync(actor.ActorId, id, ct);
        var final = await Scope(evaluation, false, ct);
        if (final.Report.Metadata.ProposalId != proposal) throw new UnauthorizedAccessException();
        var last = selected.LastOrDefault();
        return new(items, page.Length > limit && last != null ? new RegulatoryFollowupCursor(last.Row.CreatedAtUtc, last.Row.Id).Encode() : null);
    }
    public async Task<RegulatoryFollowupSaved> RecordAsync(Guid evaluation, RecordRegulatoryFollowup input, CancellationToken ct)
    {
        await Scope(evaluation, true, ct);
        input = RegulatoryFollowupInput.Validate(input);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Two parameterized, short local locks: serialize cross-stream same keys, then the evaluation/rule chain.
        // No model, network processing or reassessment runs in this transaction.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"c4-regulatory-followup-key:" + actor.ActorId + ":" + input.SubmissionKey},0))", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"c4-regulatory-followup:" + evaluation + ":" + input.RuleCode},0))", ct);
        var scope = await Scope(evaluation, true, ct);
        var finding = Finding(scope.Report, input.RuleCode);
        RegulatoryFollowupInput.ValidateFinding(input, finding);
        var findingHash = RegulatoryFollowupInput.FindingHash(finding);
        var hash = RegulatoryJson.Hash(JsonSerializer.Serialize(new { evaluation, input, findingHash }, RegulatoryJson.Options));
        var replay = await Rows.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.ActorId && x.SubmissionKey == input.SubmissionKey, ct);
        var previous = await Rows.AsNoTracking().Where(x => x.EvaluationId == evaluation && x.RuleCode == input.RuleCode)
            .OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        if (replay != null)
        {
            if (replay.InputSha256 != hash) throw new RegulatoryFollowupConflictException();
            await Scope(evaluation, true, ct);
            return new(View(replay, scope.Report), previous?.Revision ?? 0, true);
        }
        if ((previous?.Revision ?? 0) != input.ExpectedRevision) throw new RegulatoryFollowupConflictException();
        var stream = await Rows.AsNoTracking().Where(x => x.EvaluationId == evaluation && x.RuleCode == input.RuleCode).ToArrayAsync(ct);
        if (stream.Any(x => x.Operation == FindingClosurePolicy.Operation))
            throw new ArgumentException("This finding is already Verified & Closed. No further action or duplicate closure is permitted; retain the saved history.");
        if (input.Operation == FindingClosurePolicy.Operation)
        {
            var eligibility = FindingClosurePolicy.Evaluate(finding, stream.Select(x => View(x, scope.Report)).ToArray());
            if (!eligibility.CanClose) throw new ArgumentException(eligibility.Reason);
        }
        if (await Rows.CountAsync(x => x.EvaluationId == evaluation, ct) >= 1000)
            throw new ArgumentException("History limit reached. Retain existing events and request operator review.");
        var row = new RegulatoryFollowupEntity
        {
            Id = Guid.NewGuid(), EvaluationId = evaluation, RuleCode = finding.RuleCode, RuleVersion = finding.RuleVersion,
            ActorId = actor.ActorId, SubmissionKey = input.SubmissionKey, Operation = input.Operation, ReviewOutcome = input.ReviewOutcome,
            MissingInformation = input.MissingInformation, Remarks = input.Remarks!, ReferencesJson = JsonSerializer.Serialize(input.References),
            Revision = input.ExpectedRevision + 1, PreviousEventId = previous?.Id, InputSha256 = hash, OriginalFindingSha256 = findingHash
        };
        Rows.Add(row);
        await Scope(evaluation, true, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await Scope(evaluation, true, ct);
        return new(View(row, scope.Report), row.Revision, false);
    }
}

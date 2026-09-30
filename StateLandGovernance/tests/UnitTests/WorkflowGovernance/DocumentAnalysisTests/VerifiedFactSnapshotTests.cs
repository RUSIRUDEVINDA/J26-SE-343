namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentAnalysisTests;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public class VerifiedFactSnapshotTests
{
    private readonly DocumentAnalysis _analysis;
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly DateTime _utcNow = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly AnalysisRunId _runId = new AnalysisRunId(Guid.NewGuid());
    private readonly AnalysisRunResultId _resultId = new AnalysisRunResultId(Guid.NewGuid());

    private VerifiedAuthoritySnapshot _authority;

    public VerifiedFactSnapshotTests()
    {
        _analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            new GovernedDocumentId(Guid.NewGuid()),
            new DocumentVersionId(Guid.NewGuid()),
            new DocumentChecksum("SHA256", "hash"),
            1,
            1,
            _utcNow.AddDays(-10));

        var model = new AnalysisModelReference("provider", "model", "v1");
        _analysis.RequestRun(_runId, model, new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-9));
        _analysis.StartRun(_runId, _utcNow.AddDays(-8));

        _authority = new VerifiedAuthoritySnapshot(
            _actorId,
            new[] { "FactVerifier", "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, _analysis.GovernedDocumentId.Value.ToString("D")),
            _utcNow.AddDays(-20),
            _utcNow.AddDays(-10),
            _utcNow.AddDays(20));
    }

    private void CompleteRunWithFacts(params ExtractedFactInput[] facts)
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), facts, _utcNow.AddDays(-7));
    }

    private void CompleteRunWithNoFindings()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow.AddDays(-7));
    }

    private ExtractedFactInput CreateFactInput(string code, AnalysisFactValue value)
    {
        return new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode(code), value, null, null);
    }

    [Fact]
    public void PublishSnapshot_ConfirmedFact_UsesOriginalValue()
    {
        var input = CreateFactInput("C1", new AnalysisFactValue(AnalysisFactValueKind.Text, "Orig"));
        CompleteRunWithFacts(input);

        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, input.Id, FactVerificationDecision.Confirmed, null, null, _actorId, _utcNow.AddDays(-6), _authority);

        var snapshotId = new VerifiedFactSnapshotId(Guid.NewGuid());
        _analysis.PublishVerifiedFactSnapshot(snapshotId, _resultId, _actorId, _utcNow, _authority);

        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        Assert.Equal(VerifiedFactSnapshotOutcome.VerifiedFacts, snapshot.SnapshotOutcome);
        var entry = snapshot.Entries.Single();
        Assert.Equal("Orig", entry.EffectiveValue.CanonicalValue);
        Assert.Equal(FactVerificationDecision.Confirmed, entry.Decision);
    }

    [Fact]
    public void PublishSnapshot_CorrectedFact_UsesCorrectedValue()
    {
        var input = CreateFactInput("C1", new AnalysisFactValue(AnalysisFactValueKind.Text, "Orig"));
        CompleteRunWithFacts(input);

        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, input.Id, FactVerificationDecision.Corrected, new AnalysisFactValue(AnalysisFactValueKind.Text, "Corr"), "ok", _actorId, _utcNow.AddDays(-6), _authority);

        var snapshotId = new VerifiedFactSnapshotId(Guid.NewGuid());
        _analysis.PublishVerifiedFactSnapshot(snapshotId, _resultId, _actorId, _utcNow, _authority);

        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        var entry = snapshot.Entries.Single();
        Assert.Equal("Corr", entry.EffectiveValue.CanonicalValue);
        Assert.Equal(FactVerificationDecision.Corrected, entry.Decision);
    }

    [Fact]
    public void PublishSnapshot_UnsupportedFact_OmitsEntry()
    {
        var input = CreateFactInput("C1", new AnalysisFactValue(AnalysisFactValueKind.Text, "Orig"));
        CompleteRunWithFacts(input);

        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, input.Id, FactVerificationDecision.Unsupported, null, "ok", _actorId, _utcNow.AddDays(-6), _authority);

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);

        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public void PublishSnapshot_UnsupportedFact_IncrementsUnsupportedCount()
    {
        var input = CreateFactInput("C1", new AnalysisFactValue(AnalysisFactValueKind.Text, "Orig"));
        CompleteRunWithFacts(input);

        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, input.Id, FactVerificationDecision.Unsupported, null, "ok", _actorId, _utcNow.AddDays(-6), _authority);

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);

        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        Assert.Equal(1, snapshot.UnsupportedFactCount);
        Assert.Equal(1, snapshot.SourceFactCount);
        Assert.Equal(0, snapshot.PublishedFactCount);
    }

    [Fact]
    public void PublishSnapshot_MixedDecisions_ProducesCorrectCounts()
    {
        var f1 = CreateFactInput("C1", new AnalysisFactValue(AnalysisFactValueKind.Text, "V1"));
        var f2 = CreateFactInput("C2", new AnalysisFactValue(AnalysisFactValueKind.Text, "V2"));
        var f3 = CreateFactInput("C3", new AnalysisFactValue(AnalysisFactValueKind.Text, "V3"));
        CompleteRunWithFacts(f1, f2, f3);

        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, f1.Id, FactVerificationDecision.Confirmed, null, null, _actorId, _utcNow.AddDays(-6), _authority);
        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, f2.Id, FactVerificationDecision.Corrected, new AnalysisFactValue(AnalysisFactValueKind.Text, "C"), "ok", _actorId, _utcNow.AddDays(-6), _authority);
        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, f3.Id, FactVerificationDecision.Unsupported, null, "ok", _actorId, _utcNow.AddDays(-6), _authority);

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);

        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        Assert.Equal(1, snapshot.ConfirmedFactCount);
        Assert.Equal(1, snapshot.CorrectedFactCount);
        Assert.Equal(1, snapshot.UnsupportedFactCount);
        Assert.Equal(2, snapshot.PublishedFactCount);
        Assert.Equal(3, snapshot.SourceFactCount);
        Assert.Equal(2, snapshot.Entries.Count);
    }

    [Fact]
    public void PublishSnapshot_RepeatedFactCodes_PreservesSeparateEntries()
    {
        var f1 = CreateFactInput("C1", new AnalysisFactValue(AnalysisFactValueKind.Text, "V1"));
        var f2 = CreateFactInput("C1", new AnalysisFactValue(AnalysisFactValueKind.Text, "V2"));
        CompleteRunWithFacts(f1, f2);

        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, f1.Id, FactVerificationDecision.Confirmed, null, null, _actorId, _utcNow.AddDays(-6), _authority);
        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, f2.Id, FactVerificationDecision.Confirmed, null, null, _actorId, _utcNow.AddDays(-6), _authority);

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);

        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        Assert.Equal(2, snapshot.Entries.Count);
    }

    [Fact]
    public void PublishSnapshot_RepeatedFactCodes_OrdersByFactCodeThenFactId()
    {
        var id1 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var id2 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var f1 = new ExtractedFactInput(new ExtractedFactId(id1), new FactCode("C1"), new AnalysisFactValue(AnalysisFactValueKind.Text, "V1"), null, null);
        var f2 = new ExtractedFactInput(new ExtractedFactId(id2), new FactCode("C1"), new AnalysisFactValue(AnalysisFactValueKind.Text, "V2"), null, null);

        CompleteRunWithFacts(f1, f2);

        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, f1.Id, FactVerificationDecision.Confirmed, null, null, _actorId, _utcNow.AddDays(-6), _authority);
        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, f2.Id, FactVerificationDecision.Confirmed, null, null, _actorId, _utcNow.AddDays(-6), _authority);

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);

        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        Assert.Equal(id2, snapshot.Entries.First().SourceExtractedFactId.Value);
        Assert.Equal(id1, snapshot.Entries.Last().SourceExtractedFactId.Value);
    }

    [Fact]
    public void PublishSnapshot_NoFindings_ProducesNoFindingsOutcome()
    {
        CompleteRunWithNoFindings();
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        Assert.Equal(VerifiedFactSnapshotOutcome.NoFindings, _analysis.VerifiedFactSnapshots.Single().SnapshotOutcome);
        Assert.Equal(0, _analysis.VerifiedFactSnapshots.Single().SourceFactCount);
    }

    [Fact]
    public void PublishSnapshot_ArtifactOnlyOutputsProduced_ProducesNoSupportedFacts()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, new[] { new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "kind", "storageRef", "application/pdf", new AnalysisArtifactChecksum("SHA256", "hash")) }, Array.Empty<ExtractedFactInput>(), _utcNow.AddDays(-7));
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        Assert.Equal(VerifiedFactSnapshotOutcome.NoSupportedFacts, _analysis.VerifiedFactSnapshots.Single().SnapshotOutcome);
    }

    [Fact]
    public void PublishSnapshot_AllUnsupported_ProducesNoSupportedFacts()
    {
        var input = CreateFactInput("C1", new AnalysisFactValue(AnalysisFactValueKind.Text, "Orig"));
        CompleteRunWithFacts(input);

        _analysis.RecordFactVerification(new HumanFactVerificationId(Guid.NewGuid()), _resultId, input.Id, FactVerificationDecision.Unsupported, null, "ok", _actorId, _utcNow.AddDays(-6), _authority);

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);

        Assert.Equal(VerifiedFactSnapshotOutcome.NoSupportedFacts, _analysis.VerifiedFactSnapshots.Single().SnapshotOutcome);
    }

        [Fact]
    public void VerifiedFactSnapshotPublished_HasExactPropertyTypeMap()
    {
        var props = typeof(VerifiedFactSnapshotPublished).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var map = props.ToDictionary(p => p.Name, p => p.PropertyType);

        Assert.Equal(26, map.Count);
        Assert.Equal(typeof(Guid), map["EventId"]);
        Assert.Equal(typeof(DateTime), map["OccurredOn"]);
        Assert.Equal(typeof(DocumentAnalysisId), map["DocumentAnalysisId"]);
        Assert.Equal(typeof(GovernedDocumentId), map["GovernedDocumentId"]);
        Assert.Equal(typeof(DocumentVersionId), map["DocumentVersionId"]);
        Assert.Equal(typeof(string), map["DocumentChecksumAlgorithm"]);
        Assert.Equal(typeof(string), map["DocumentChecksumValue"]);
        Assert.Equal(typeof(AnalysisRunId), map["AnalysisRunId"]);
        Assert.Equal(typeof(AnalysisRunResultId), map["AnalysisRunResultId"]);
        Assert.Equal(typeof(VerifiedFactSnapshotId), map["VerifiedFactSnapshotId"]);
        Assert.Equal(typeof(int), map["RunNumber"]);
        Assert.Equal(typeof(AnalysisResultOutcome), map["ResultOutcome"]);
        Assert.Equal(typeof(VerifiedFactSnapshotOutcome), map["SnapshotOutcome"]);
        Assert.Equal(typeof(int), map["SourceFactCount"]);
        Assert.Equal(typeof(int), map["PublishedFactCount"]);
        Assert.Equal(typeof(int), map["ConfirmedFactCount"]);
        Assert.Equal(typeof(int), map["CorrectedFactCount"]);
        Assert.Equal(typeof(int), map["UnsupportedFactCount"]);
        Assert.Equal(typeof(Guid), map["PublishingActorId"]);
        Assert.Equal(typeof(string), map["VerifiedCapability"]);
        Assert.Equal(typeof(AuthorityScopeKind), map["GrantedAuthorityScopeKind"]);
        Assert.Equal(typeof(string), map["GrantedAuthorityScopeIdentifier"]);
        Assert.Equal(typeof(AuthorityScopeKind), map["RequiredAuthorityScopeKind"]);
        Assert.Equal(typeof(string), map["RequiredAuthorityScopeIdentifier"]);
        Assert.Equal(typeof(DateTime), map["AuthorityVerificationTime"]);
        Assert.Equal(typeof(int), map["DocumentAnalysisRevision"]);
    }
}

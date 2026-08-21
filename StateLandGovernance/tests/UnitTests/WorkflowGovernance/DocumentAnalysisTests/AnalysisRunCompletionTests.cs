namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentAnalysisTests;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using Xunit;

public class AnalysisRunCompletionTests
{
    private readonly DocumentAnalysis _analysis;
    private readonly AnalysisRunId _runId = new(Guid.NewGuid());
    private readonly AnalysisRunResultId _resultId = new(Guid.NewGuid());
    private readonly DateTime _utcNow = new(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    
    public AnalysisRunCompletionTests()
    {
        _analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            new GovernedDocumentId(Guid.NewGuid()),
            new DocumentVersionId(Guid.NewGuid()),
            new DocumentChecksum("SHA256", "val"),
            1,
            1,
            _utcNow
        );
        _analysis.RequestRun(_runId, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _utcNow);
        _analysis.StartRun(_runId, _utcNow);
    }

    [Fact] public void CompleteRun_ArtifactOnly_OutputsProduced_Transitions()
    {
        var artifact = new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "kind", "storage", "text/plain", new AnalysisArtifactChecksum("SHA256", "abc"));
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, new[] { artifact }, Array.Empty<ExtractedFactInput>(), _utcNow);
        Assert.Equal(AnalysisRunState.Completed, _analysis.Runs.Single().State);
    }

    [Fact] public void CompleteRun_FactsOnly_OutputsProduced_Transitions()
    {
        var fact = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("code"), new AnalysisFactValue(AnalysisFactValueKind.Text, "val"), null, null);
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), new[] { fact }, _utcNow);
        Assert.Equal(AnalysisRunState.Completed, _analysis.Runs.Single().State);
    }

    [Fact] public void CompleteRun_ArtifactsAndFacts_Transitions()
    {
        var artifact = new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "kind", "storage", "text/plain", new AnalysisArtifactChecksum("SHA256", "abc"));
        var fact = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("code"), new AnalysisFactValue(AnalysisFactValueKind.Text, "val"), null, null);
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, new[] { artifact }, new[] { fact }, _utcNow);
        Assert.Equal(AnalysisRunState.Completed, _analysis.Runs.Single().State);
    }
    
    [Fact] public void CompleteRun_ZeroOutput_NoFindings_Transitions()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow);
        Assert.Equal(AnalysisRunState.Completed, _analysis.Runs.Single().State);
    }

    [Fact] public void CompleteRun_NoFindings_WithDiagnosticArtifacts_Transitions()
    {
        var artifact = new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "kind", "storage", "text/plain", new AnalysisArtifactChecksum("SHA256", "abc"));
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, new[] { artifact }, Array.Empty<ExtractedFactInput>(), _utcNow);
        Assert.Equal(AnalysisRunState.Completed, _analysis.Runs.Single().State);
    }

    [Fact] public void CompleteRun_OutputsProduced_ZeroOutputs_ThrowsInvalidResult()
    {
        Assert.Throws<InvalidAnalysisRunResultException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow));
    }

    [Fact] public void CompleteRun_NoFindings_WithFacts_ThrowsInvalidResult()
    {
        var fact = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("code"), new AnalysisFactValue(AnalysisFactValueKind.Text, "val"), null, null);
        Assert.Throws<InvalidAnalysisRunResultException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), new[] { fact }, _utcNow));
    }

    [Fact] public void CompleteRun_UndefinedOutcome_ThrowsInvalidResult()
    {
        var artifact = new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "kind", "storage", "text/plain", new AnalysisArtifactChecksum("SHA256", "abc"));
        Assert.Throws<InvalidAnalysisRunResultException>(() => _analysis.CompleteRun(_runId, _resultId, (AnalysisResultOutcome)999, new[] { artifact }, Array.Empty<ExtractedFactInput>(), _utcNow));
    }

    [Fact] public void EmptyResultId_Throws() { Assert.Throws<InvalidAnalysisRunResultException>(() => new AnalysisRunResultId(Guid.Empty)); }
    [Fact] public void EmptyArtifactId_Throws() { Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactId(Guid.Empty)); }
    [Fact] public void EmptyFactId_Throws() { Assert.Throws<InvalidExtractedFactException>(() => new ExtractedFactId(Guid.Empty)); }

    [Fact] public void DuplicateResultId_AcrossRuns_Throws()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow);
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("P", "M2", "V"), new[] { new AnalysisCapabilityCode("C") }, _utcNow);
        _analysis.StartRun(runId2, _utcNow);
        Assert.Throws<DuplicateAnalysisRunResultException>(() => _analysis.CompleteRun(runId2, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow));
    }

    [Fact] public void DuplicateArtifactId_Throws()
    {
        var artId = new AnalysisResultArtifactId(Guid.NewGuid());
        var artifact1 = new AnalysisResultArtifactReference(artId, "kind", "storage", "text/plain", new AnalysisArtifactChecksum("SHA256", "abc"));
        var artifact2 = new AnalysisResultArtifactReference(artId, "kind", "storage2", "text/plain", new AnalysisArtifactChecksum("SHA256", "abc"));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, new[] { artifact1, artifact2 }, Array.Empty<ExtractedFactInput>(), _utcNow));
    }

    [Fact] public void DuplicateFactId_Throws()
    {
        var factId = new ExtractedFactId(Guid.NewGuid());
        var fact1 = new ExtractedFactInput(factId, new FactCode("code"), new AnalysisFactValue(AnalysisFactValueKind.Text, "v1"), null, null);
        var fact2 = new ExtractedFactInput(factId, new FactCode("code2"), new AnalysisFactValue(AnalysisFactValueKind.Text, "v2"), null, null);
        Assert.Throws<InvalidExtractedFactException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), new[] { fact1, fact2 }, _utcNow));
    }

    [Fact] public void RepeatedFactCode_Accepted()
    {
        var fact1 = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("code"), new AnalysisFactValue(AnalysisFactValueKind.Text, "v1"), null, null);
        var fact2 = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("code"), new AnalysisFactValue(AnalysisFactValueKind.Text, "v2"), null, null);
        var run = _analysis.Runs.Single();
        Assert.NotNull(run.Result);
        Assert.Equal(2, run.Result.ExtractedFacts.Count);
    }

    [Fact] public void AnalysisFactValue_Kinds_Valid()
    {
        Assert.Equal("abc", new AnalysisFactValue(AnalysisFactValueKind.Text, " abc ").CanonicalValue);
        Assert.Equal("id1", new AnalysisFactValue(AnalysisFactValueKind.Identifier, " id1 ").CanonicalValue);
        Assert.Equal("123", new AnalysisFactValue(AnalysisFactValueKind.Integer, "123").CanonicalValue);
        Assert.Equal("-1.5", new AnalysisFactValue(AnalysisFactValueKind.Decimal, "-1.5").CanonicalValue);
        Assert.Equal("2023-01-01", new AnalysisFactValue(AnalysisFactValueKind.Date, "2023-01-01").CanonicalValue);
        Assert.Equal("true", new AnalysisFactValue(AnalysisFactValueKind.Boolean, "true").CanonicalValue);
    }

    [Fact] public void AnalysisFactValue_UndefinedKind_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue((AnalysisFactValueKind)999, "v")); }
    [Fact] public void AnalysisFactValue_EmptyText_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Text, " ")); }
    [Fact] public void AnalysisFactValue_MaxLength_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Text, new string('a', 1001))); }
    
    [Fact] public void AnalysisFactValue_Text_LF_CR_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Text, "a\nb")); Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Text, "a\rb")); }
    [Fact] public void AnalysisFactValue_Identifier_LF_Tab_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Identifier, "a\nb")); Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Identifier, "a\tb")); }

    [Fact] public void AnalysisFactValue_Integer_NonCanonical_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Integer, "+123")); }
    [Fact] public void AnalysisFactValue_Decimal_NonCanonical_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Decimal, "1.50")); }
    [Fact] public void AnalysisFactValue_Date_NonStrict_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Date, "2023/01/01")); }
    [Fact] public void AnalysisFactValue_Boolean_NonLower_Throws() { Assert.Throws<InvalidAnalysisFactValueException>(() => new AnalysisFactValue(AnalysisFactValueKind.Boolean, "True")); }

    [Fact] public void Artifact_Validations()
    {
        var id = new AnalysisResultArtifactId(Guid.NewGuid());
        var c = new AnalysisArtifactChecksum("SHA256", "V");
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "k!nd", "store", "text/plain", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "http://store", "text/plain", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store:a", "text/plain", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "st?ore", "text/plain", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "/store", "text/plain", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store\\a", "text/plain", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store//a", "text/plain", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store/../a", "text/plain", c));
        
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/plain;a", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "t/p/a", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "t/ ", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "/json", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text//plain", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text plain/json", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/@", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "@/json", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/(plain)", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/plain;charset=utf-8", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/{json}", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/json}", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "{text}/json", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/plain{bad}", c));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisResultArtifactReference(id, "kind", "store", "text/plain; charset=utf-8", c));
        
        var valid1 = new AnalysisResultArtifactReference(id, "kind", "store", "text/plain", c);
        var valid2 = new AnalysisResultArtifactReference(id, "kind", "store", "application/json", c);
        var valid3 = new AnalysisResultArtifactReference(id, "kind", "store", "application/vnd.example+json", c);
        
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisArtifactChecksum("", "v"));
        Assert.Throws<InvalidAnalysisResultArtifactException>(() => new AnalysisArtifactChecksum("a", ""));
    }

    [Fact] public void Confidence_Validations()
    {
        Assert.Equal(0.0m, new ConfidenceScore(0.0m).Value);
        Assert.Equal(1.0m, new ConfidenceScore(1.0m).Value);
        Assert.Throws<InvalidExtractedFactException>(() => new ConfidenceScore(-0.1m));
        Assert.Throws<InvalidExtractedFactException>(() => new ConfidenceScore(1.1m));
    }

    [Fact] public void Evidence_Validations()
    {
        var aid = new AnalysisResultArtifactId(Guid.NewGuid());
        Assert.Equal("a\nb", new AnalysisEvidenceReference(aid, 1, "a\r\nb").Excerpt);
        Assert.Equal("a\nb", new AnalysisEvidenceReference(aid, 1, "a\rb").Excerpt);
        Assert.Throws<InvalidAnalysisEvidenceException>(() => new AnalysisEvidenceReference(aid, 0, null));
        Assert.Throws<InvalidAnalysisEvidenceException>(() => new AnalysisEvidenceReference(aid, 1, new string('a', 501)));
        Assert.Throws<InvalidAnalysisEvidenceException>(() => new AnalysisEvidenceReference(aid, 1, "a\tb"));

        var fact = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("c"), new AnalysisFactValue(AnalysisFactValueKind.Text, "v"), null, new AnalysisEvidenceReference(aid, 1, "e"));
        Assert.Throws<InvalidAnalysisEvidenceException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), new[] { fact }, _utcNow));
    }

    [Fact] public void NullCollections_Throws()
    {
        Assert.Throws<InvalidAnalysisRunResultException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, null!, Array.Empty<ExtractedFactInput>(), _utcNow));
        Assert.Throws<InvalidAnalysisRunResultException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), null!, _utcNow));
    }

    [Fact] public void NullElements_Throws()
    {
        Assert.Throws<InvalidAnalysisRunResultException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, new AnalysisResultArtifactReference[] { null! }, Array.Empty<ExtractedFactInput>(), _utcNow));
        Assert.Throws<InvalidAnalysisRunResultException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), new ExtractedFactInput[] { null! }, _utcNow));
    }

    [Fact] public void CallerListMutation_DoesNotChangeResult()
    {
        var list = new List<AnalysisResultArtifactReference>();
        var facts = new List<ExtractedFactInput>();
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, list, facts, _utcNow);
        list.Add(new AnalysisResultArtifactReference(new AnalysisResultArtifactId(Guid.NewGuid()), "k", "s", "text/plain", new AnalysisArtifactChecksum("a", "b")));
        facts.Add(new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("c"), new AnalysisFactValue(AnalysisFactValueKind.Text, "v"), null, null));
        var run = _analysis.Runs.Single();
        Assert.NotNull(run.Result);
        Assert.Empty(run.Result.Artifacts);
        Assert.Empty(run.Result.ExtractedFacts);
    }

    [Fact] public void Result_ExposedCollections_AreImmutable()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow);
        var r = _analysis.Runs.Single().Result;
        Assert.NotNull(r);
        Assert.Throws<NotSupportedException>(() => ((IList<AnalysisResultArtifactReference>)r.Artifacts).Add(null!));
        Assert.Throws<NotSupportedException>(() => ((IList<ExtractedFact>)r.ExtractedFacts).Add(null!));
        Assert.Throws<NotSupportedException>(() => ((IList<AnalysisCapabilityCode>)r.RequestedCapabilities).Add(new AnalysisCapabilityCode("C2")));
    }

    [Fact] public void CompleteRun_FromFailed_ThrowsInvalidAnalysisRunTransitionException()
    {
        var r2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(r2, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _utcNow);
        _analysis.StartRun(r2, _utcNow);
        _analysis.FailRun(r2, new AnalysisRunFailure("C", "D"), _utcNow);
        var run = _analysis.Runs.Single(r => r.Id.Value == r2.Value);
        var initialRev = _analysis.Revision;
        var initialEvents = _analysis.DomainEvents.Count;
        var initialCompletedEvents = _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Count();
        var initialState = run.State;
        var initialResult = run.Result;
        var initialCompletedAt = run.CompletedAt;
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => _analysis.CompleteRun(r2, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow));
        Assert.Equal(initialRev, _analysis.Revision);
        Assert.Equal(initialEvents, _analysis.DomainEvents.Count);
        Assert.Equal(initialCompletedEvents, _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Count());
        Assert.Equal(initialState, run.State);
        Assert.Equal(initialResult, run.Result);
        Assert.Equal(initialCompletedAt, run.CompletedAt);
    }

    [Fact] public void CompleteRun_FromSuperseded_ThrowsInvalidAnalysisRunTransitionException()
    {
        var r2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(r2, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _utcNow);
        _analysis.StartRun(r2, _utcNow);
        _analysis.SupersedeRun(r2, "Reason", _utcNow);
        var run = _analysis.Runs.Single(r => r.Id.Value == r2.Value);
        var initialRev = _analysis.Revision;
        var initialEvents = _analysis.DomainEvents.Count;
        var initialCompletedEvents = _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Count();
        var initialState = run.State;
        var initialResult = run.Result;
        var initialCompletedAt = run.CompletedAt;
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => _analysis.CompleteRun(r2, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow));
        Assert.Equal(initialRev, _analysis.Revision);
        Assert.Equal(initialEvents, _analysis.DomainEvents.Count);
        Assert.Equal(initialCompletedEvents, _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Count());
        Assert.Equal(initialState, run.State);
        Assert.Equal(initialResult, run.Result);
        Assert.Equal(initialCompletedAt, run.CompletedAt);
    }

    [Fact] public void Bindings_AreCopiedFromRun()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow);
        var r = _analysis.Runs.Single().Result;
        Assert.NotNull(r);
        Assert.Equal(_runId, r.AnalysisRunId);
        Assert.Equal(_analysis.DocumentVersionId, r.DocumentVersionId);
        Assert.Equal(_analysis.DocumentChecksum, r.DocumentChecksum);
        Assert.Equal("P", r.ModelReference.Provider);
        Assert.Equal("C", r.RequestedCapabilities.Single().Value);
    }

    [Fact] public void Transition_FromRequested_Throws()
    {
        var r2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(r2, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _utcNow);
        var run = _analysis.Runs.Single(r => r.Id.Value == r2.Value);
        var initialRev = _analysis.Revision;
        var initialEvents = _analysis.DomainEvents.Count;
        var initialCompletedEvents = _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Count();
        var initialState = run.State;
        var initialResult = run.Result;
        var initialCompletedAt = run.CompletedAt;
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => _analysis.CompleteRun(r2, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow));
        Assert.Equal(initialRev, _analysis.Revision);
        Assert.Equal(initialEvents, _analysis.DomainEvents.Count);
        Assert.Equal(initialCompletedEvents, _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Count());
        Assert.Equal(initialState, run.State);
        Assert.Equal(initialResult, run.Result);
        Assert.Equal(initialCompletedAt, run.CompletedAt);
    }

    [Fact] public void Transition_FromCompleted_Throws()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow);
        var run = _analysis.Runs.Single(r => r.Id.Value == _runId.Value);
        var initialRev = _analysis.Revision;
        var initialEvents = _analysis.DomainEvents.Count;
        var initialCompletedEvents = _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Count();
        var initialState = run.State;
        var initialResult = run.Result;
        var initialCompletedAt = run.CompletedAt;
        Assert.NotNull(run.Result);
        var initialResultId = run.Result.Id;
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => _analysis.CompleteRun(_runId, new AnalysisRunResultId(Guid.NewGuid()), AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow));
        Assert.Equal(initialRev, _analysis.Revision);
        Assert.Equal(initialEvents, _analysis.DomainEvents.Count);
        Assert.Equal(initialCompletedEvents, _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Count());
        Assert.Equal(initialState, run.State);
        Assert.Equal(initialResult, run.Result);
        Assert.Equal(initialCompletedAt, run.CompletedAt);
        Assert.Equal(initialResultId, run.Result.Id);
    }

    [Fact] public void NonUtc_Throws() { Assert.Throws<InvalidAnalysisRunTransitionException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), DateTime.Now)); }
    
    [Fact] public void Chronology_BeforeStarted_Throws() { Assert.Throws<InvalidAnalysisRunTransitionException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow.AddSeconds(-1))); }
    
    [Fact] public void Chronology_EqualStarted_Accepted()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow);
        Assert.Equal(AnalysisRunState.Completed, _analysis.Runs.Single().State);
    }

    [Fact] public void Atomicity_RevisionAndEvents()
    {
        var initialRev = _analysis.Revision;
        var initialEvents = _analysis.DomainEvents.Count;
        Assert.Throws<InvalidAnalysisRunResultException>(() => _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow));
        Assert.Equal(initialRev, _analysis.Revision);
        Assert.Equal(initialEvents, _analysis.DomainEvents.Count);
        Assert.Null(_analysis.Runs.Single().Result);
        Assert.Equal(AnalysisRunState.Running, _analysis.Runs.Single().State);
    }

    [Fact] public void CompleteRun_Success_IncrementsRevisionExactlyOnce()
    {
        var initialRev = _analysis.Revision;
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow);
        Assert.Equal(initialRev + 1, _analysis.Revision);
    }

    [Fact] public void CompleteRun_Success_ExactEvent()
    {
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow);
        var evt = _analysis.DomainEvents.OfType<AnalysisRunCompleted>().Single();
        Assert.Equal(_utcNow, evt.OccurredOn);
        Assert.Equal(_analysis.Id, evt.DocumentAnalysisId);
        Assert.Equal(_runId, evt.AnalysisRunId);
        Assert.Equal(_resultId, evt.AnalysisRunResultId);
        Assert.Equal(AnalysisResultOutcome.NoFindings, evt.Outcome);
        Assert.Equal(0, evt.ArtifactCount);
        Assert.Equal(0, evt.ExtractedFactCount);
        Assert.Equal(_analysis.Revision, evt.DocumentAnalysisRevision);
        Assert.Equal("C", evt.RequestedCapabilities.Single());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)evt.RequestedCapabilities).Add("a"));
    }

    [Fact] public void Event_HasExactContract()
    {
        var props = typeof(AnalysisRunCompleted).GetProperties().ToDictionary(p => p.Name, p => p.PropertyType);
        
        var expected = new Dictionary<string, Type>
        {
            { "EventId", typeof(Guid) },
            { "OccurredOn", typeof(DateTime) },
            { "DocumentAnalysisId", typeof(DocumentAnalysisId) },
            { "AnalysisRunId", typeof(AnalysisRunId) },
            { "AnalysisRunResultId", typeof(AnalysisRunResultId) },
            { "GovernedDocumentId", typeof(GovernedDocumentId) },
            { "DocumentVersionId", typeof(DocumentVersionId) },
            { "ChecksumAlgorithm", typeof(string) },
            { "ChecksumValue", typeof(string) },
            { "RunNumber", typeof(int) },
            { "ModelProvider", typeof(string) },
            { "ModelName", typeof(string) },
            { "ModelVersion", typeof(string) },
            { "RequestedCapabilities", typeof(IReadOnlyCollection<string>) },
            { "Outcome", typeof(AnalysisResultOutcome) },
            { "ArtifactCount", typeof(int) },
            { "ExtractedFactCount", typeof(int) },
            { "DocumentAnalysisRevision", typeof(int) }
        };

        Assert.Equal(expected.Count, props.Count);
        foreach (var kvp in expected)
        {
            Assert.True(props.ContainsKey(kvp.Key), $"Missing property {kvp.Key}");
            Assert.Equal(kvp.Value, props[kvp.Key]);
        }
    }

    [Fact] public void NoPublicInitSetters()
    {
        var types = new[] { typeof(AnalysisRunResult), typeof(ExtractedFact), typeof(AnalysisResultArtifactReference), typeof(AnalysisEvidenceReference), typeof(ExtractedFactInput), typeof(AnalysisFactValue), typeof(ConfidenceScore), typeof(FactCode) };
        foreach (var t in types)
        {
            var props = t.GetProperties();
            foreach (var p in props)
            {
                Assert.False(p.CanWrite, $"Property {p.Name} on {t.Name} can be written.");
            }
        }
    }

    [Fact] public void NoDynamicObjectJsonProps()
    {
        var types = new[] { typeof(AnalysisRunResult), typeof(ExtractedFact), typeof(AnalysisResultArtifactReference), typeof(AnalysisEvidenceReference), typeof(ExtractedFactInput), typeof(AnalysisFactValue), typeof(ConfidenceScore), typeof(FactCode) };
        foreach (var t in types)
        {
            var props = t.GetProperties();
            foreach (var p in props)
            {
                Assert.NotEqual(typeof(object), p.PropertyType);
                Assert.False(p.PropertyType.Name.Contains("Dictionary"), $"Type {t.Name} contains dictionary");
                Assert.False(p.PropertyType.Name.Contains("JsonElement"), $"Type {t.Name} contains JSON element");
                Assert.False(p.PropertyType.Name.Contains("JsonDocument"), $"Type {t.Name} contains JSON document");
            }
        }
    }
}





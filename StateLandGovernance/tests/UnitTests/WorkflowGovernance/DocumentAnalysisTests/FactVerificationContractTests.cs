using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentAnalysisTests;

public class FactVerificationContractTests
{
    private readonly DocumentAnalysis _analysis;
    private readonly AnalysisRunId _runId = new(Guid.NewGuid());
    private readonly AnalysisRunResultId _resultId = new(Guid.NewGuid());
    private readonly ExtractedFactId _factId = new(Guid.NewGuid());
    private readonly Guid _verifyingActorId = Guid.NewGuid();
    private readonly VerifiedAuthoritySnapshot _authority;
    private readonly DateTime _createdAt = DateTime.UtcNow.AddHours(-2);
    private readonly DateTime _runRequestedAt = DateTime.UtcNow.AddHours(-1);
    private readonly DateTime _runCompletedAt = DateTime.UtcNow.AddMinutes(-30);
    private readonly DateTime _verifiedAt = DateTime.UtcNow.AddMinutes(-10);

    public FactVerificationContractTests()
    {
        var docId = new GovernedDocumentId(Guid.NewGuid());
        var versionId = new DocumentVersionId(Guid.NewGuid());
        _analysis = new DocumentAnalysis(new DocumentAnalysisId(Guid.NewGuid()), docId, versionId, new DocumentChecksum("SHA-256", "ABC"), 1, 1, _createdAt);
        _analysis.RequestRun(_runId, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt);
        _analysis.StartRun(_runId, _runRequestedAt.AddMinutes(1));

        var factValue = new AnalysisFactValue(AnalysisFactValueKind.Text, "Original");
        var facts = new[] { new ExtractedFactInput(_factId, new FactCode("FC"), factValue, null, null) };
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), facts, _runCompletedAt);

        _authority = new VerifiedAuthoritySnapshot(
            _verifyingActorId,
            new[] { "FactVerifier" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, docId.Value.ToString("D")),
            _createdAt.AddDays(-1),
            _createdAt.AddDays(-1),
            _createdAt.AddDays(1)
        );
    }

    [Fact]
    public void RecordVerification_Duplicate_SameIdAndChangedResultIdThrowsConflicting()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        var res2Id = new AnalysisRunResultId(Guid.NewGuid());
        var fact2Id = new ExtractedFactId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        
        var facts2 = new[] { new ExtractedFactInput(fact2Id, new FactCode("FC2"), new AnalysisFactValue(AnalysisFactValueKind.Text, "A"), null, null) };
        _analysis.CompleteRun(run2Id, res2Id, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), facts2, _runCompletedAt.AddMinutes(5));

        Assert.Throws<ConflictingFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, res2Id, fact2Id, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt.AddMinutes(10), _authority));
    }

    [Fact]
    public void RecordVerification_Duplicate_SameIdAndChangedAuthorityEvidenceThrowsConflicting()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        
        var auth2 = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "FactVerifier" }, _authority.Scope, _authority.ValidFrom, _authority.VerificationTime.AddMinutes(1), _authority.ValidUntil);
        Assert.Throws<ConflictingFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, auth2));
    }

    [Fact]
    public void RecordVerification_Duplicate_SameFactInDifferentResultIsAllowed()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        var res2Id = new AnalysisRunResultId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));
        var facts2 = new[] { new ExtractedFactInput(_factId, new FactCode("FC"), new AnalysisFactValue(AnalysisFactValueKind.Text, "A"), null, null) };
        _analysis.CompleteRun(run2Id, res2Id, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), facts2, _runCompletedAt.AddMinutes(5));

        var id1 = new HumanFactVerificationId(Guid.NewGuid());
        // Verify in Result 2 (Result 1 is stale now!)
        _analysis.RecordFactVerification(id1, res2Id, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        Assert.Single(_analysis.Verifications);
    }

    [Fact]
    public void RecordVerification_Chronology_NonUtcVerifiedAtRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, DateTime.Now, _authority));
    }

    [Fact]
    public void RecordVerification_Chronology_BeforeCompletedAtRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _runCompletedAt.AddMinutes(-1), _authority));
    }

    [Fact]
    public void RecordVerification_Chronology_EqualCompletedAtAccepted()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _runCompletedAt, _authority);
        Assert.Single(_analysis.Verifications);
    }

    [Fact]
    public void RecordVerification_SuccessAndBinding_ExactlyIncrementsRevisionAndEmitsEvent()
    {
        var initialRevision = _analysis.Revision;
        var initialEventCount = _analysis.DomainEvents.Count;

        var id = new HumanFactVerificationId(Guid.NewGuid());
        var cv = new AnalysisFactValue(AnalysisFactValueKind.Text, "N");
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Corrected, cv, "R", _verifyingActorId, _verifiedAt, _authority);

        Assert.Equal(initialRevision + 1, _analysis.Revision);
        Assert.Equal(initialEventCount + 1, _analysis.DomainEvents.Count);

        var v = _analysis.Verifications.Single();
        Assert.Equal(_analysis.Id, v.DocumentAnalysisId);
        Assert.Equal(_runId, v.AnalysisRunId);
        Assert.Equal(_resultId, v.AnalysisRunResultId);
        Assert.Equal(_factId, v.ExtractedFactId);
        Assert.Equal(_analysis.DocumentVersionId, v.DocumentVersionId);
        Assert.Equal(_analysis.DocumentChecksum, v.DocumentChecksum);
        Assert.Equal(1, v.RunNumber);
        Assert.Equal("FC", v.FactCode.Value);
        Assert.Equal("Original", v.OriginalValue.CanonicalValue);
    }

    [Fact]
    public void RecordVerification_ExactEventContractTest()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);

        var evt = _analysis.DomainEvents.OfType<HumanFactVerificationRecorded>().Single();
        Assert.IsType<Guid>(evt.EventId);
        Assert.IsType<DateTime>(evt.OccurredOn);
        Assert.IsType<DocumentAnalysisId>(evt.DocumentAnalysisId);
        Assert.IsType<AnalysisRunId>(evt.AnalysisRunId);
        Assert.IsType<AnalysisRunResultId>(evt.AnalysisRunResultId);
        Assert.IsType<HumanFactVerificationId>(evt.HumanFactVerificationId);
        Assert.IsType<ExtractedFactId>(evt.ExtractedFactId);
        Assert.IsType<string>(evt.FactCode);
        Assert.IsType<FactVerificationDecision>(evt.Decision);
        Assert.IsType<Guid>(evt.VerifyingActorId);
        Assert.IsType<string>(evt.VerifiedCapability);
        Assert.IsType<AuthorityScopeKind>(evt.GrantedAuthorityScopeKind);
        Assert.IsType<string>(evt.GrantedAuthorityScopeIdentifier);
        Assert.IsType<AuthorityScopeKind>(evt.RequiredAuthorityScopeKind);
        Assert.IsType<string>(evt.RequiredAuthorityScopeIdentifier);
        Assert.IsType<DateTime>(evt.AuthorityVerificationTime);
        Assert.IsType<DocumentVersionId>(evt.DocumentVersionId);
        Assert.IsType<string>(evt.ChecksumAlgorithm);
        Assert.IsType<string>(evt.ChecksumValue);
        Assert.IsType<int>(evt.RunNumber);
        Assert.IsType<int>(evt.DocumentAnalysisRevision);

        // Prove exact property count using Reflection
        var props = typeof(HumanFactVerificationRecorded).GetProperties();
        Assert.Equal(21, props.Length);

        // Prove exclusions
        Assert.DoesNotContain(props, p => p.Name == "OriginalValue");
        Assert.DoesNotContain(props, p => p.Name == "CorrectedValue");
        Assert.DoesNotContain(props, p => p.Name == "Reason");
    }

    [Fact]
    public void RecordVerification_Atomicity_FailureLeavesStateUnchanged()
    {
        var rev = _analysis.Revision;
        var evts = _analysis.DomainEvents.Count;
        var vers = _analysis.Verifications.Count;

        var id = new HumanFactVerificationId(Guid.NewGuid());
        var ex = Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, "ReasonNotAllowed", _verifyingActorId, _verifiedAt, _authority));

        Assert.Equal(rev, _analysis.Revision);
        Assert.Equal(evts, _analysis.DomainEvents.Count);
        Assert.Equal(vers, _analysis.Verifications.Count);
    }
}

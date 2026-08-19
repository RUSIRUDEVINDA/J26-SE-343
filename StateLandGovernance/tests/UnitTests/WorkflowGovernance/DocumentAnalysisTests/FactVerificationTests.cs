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

public class FactVerificationTests
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

    public FactVerificationTests()
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
    public void RecordVerification_Confirmed_CreatesVerificationAndEvent()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);

        Assert.Single(_analysis.Verifications);
        var v = _analysis.Verifications.First();
        Assert.Equal(FactVerificationDecision.Confirmed, v.Decision);
        Assert.Null(v.CorrectedValue);
        Assert.Null(v.Reason);
        Assert.Equal("FactVerifier", v.VerifiedCapability);

        var evt = _analysis.DomainEvents.OfType<HumanFactVerificationRecorded>().Single();
        Assert.Equal(FactVerificationDecision.Confirmed, evt.Decision);
        Assert.Equal(_verifyingActorId, evt.VerifyingActorId);
    }

    [Fact]
    public void RecordVerification_NullSnapshot_ThrowsMissingVerifiedAuthorityException()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, null!));
    }

    [Fact]
    public void RecordVerification_WrongActor_ThrowsMissingVerifiedAuthorityException()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, Guid.NewGuid(), _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_UnknownResult_ThrowsAnalysisRunResultNotFoundException()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<AnalysisRunResultNotFoundException>(() =>
            _analysis.RecordFactVerification(id, new AnalysisRunResultId(Guid.NewGuid()), _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_UnknownFact_ThrowsExtractedFactNotFoundException()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<ExtractedFactNotFoundException>(() =>
            _analysis.RecordFactVerification(id, _resultId, new ExtractedFactId(Guid.NewGuid()), FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_NewerCompletedRun_ThrowsStaleAnalysisResultException()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));
        var factValue2 = new AnalysisFactValue(AnalysisFactValueKind.Text, "Another");
        var facts2 = new[] { new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("FC2"), factValue2, null, null) };
        _analysis.CompleteRun(run2Id, new AnalysisRunResultId(Guid.NewGuid()), AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), facts2, _runCompletedAt.AddMinutes(5));

        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<StaleAnalysisResultException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_DuplicateId_ThrowsDuplicateOrConflicting()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);

        // Exact duplicate
        Assert.Throws<DuplicateFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority));

        // Conflicting (different decision)
        Assert.Throws<ConflictingFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, null, "Reason", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_AlreadyVerifiedFact_ThrowsFactAlreadyVerifiedException()
    {
        var id1 = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id1, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);

        var id2 = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<FactAlreadyVerifiedException>(() =>
            _analysis.RecordFactVerification(id2, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Corrected_ValidatesProperly()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var correctedVal = new AnalysisFactValue(AnalysisFactValueKind.Text, "NewValue");
        
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Corrected, correctedVal, "Typo", _verifyingActorId, _verifiedAt, _authority);
        var v = _analysis.Verifications.First();
        Assert.Equal("NewValue", v.CorrectedValue!.CanonicalValue);
        Assert.Equal("Typo", v.Reason);
    }
}

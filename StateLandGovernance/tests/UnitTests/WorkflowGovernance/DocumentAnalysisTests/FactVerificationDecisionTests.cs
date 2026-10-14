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

public class FactVerificationDecisionTests
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

    public FactVerificationDecisionTests()
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
    public void RecordVerification_Confirmed_CorrectedValueRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var cv = new AnalysisFactValue(AnalysisFactValueKind.Text, "N");
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, cv, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Confirmed_ReasonRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, "Reason", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Corrected_MissingValueRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Corrected, null, "Reason", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Corrected_MissingReasonRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var cv = new AnalysisFactValue(AnalysisFactValueKind.Text, "N");
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Corrected, cv, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Corrected_BlankReasonRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var cv = new AnalysisFactValue(AnalysisFactValueKind.Text, "N");
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Corrected, cv, "   ", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Corrected_IncompatibleKindRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var cv = new AnalysisFactValue(AnalysisFactValueKind.Boolean, "true");
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Corrected, cv, "Reason", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Corrected_UnchangedCanonicalValueRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var cv = new AnalysisFactValue(AnalysisFactValueKind.Text, "Original");
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Corrected, cv, "Reason", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Unsupported_CorrectedValueRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var cv = new AnalysisFactValue(AnalysisFactValueKind.Text, "N");
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, cv, "Reason", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Unsupported_MissingReasonRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, null, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Unsupported_BlankReasonRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, null, "   ", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Reason_TrimmedBeforeStorage()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, null, "  TrimMe  ", _verifyingActorId, _verifiedAt, _authority);
        var v = _analysis.Verifications.First();
        Assert.Equal("TrimMe", v.Reason);
    }

    [Fact]
    public void RecordVerification_Reason_500CharsAccepted()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var r = new string('a', 500);
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, null, r, _verifyingActorId, _verifiedAt, _authority);
        var v = _analysis.Verifications.First();
        Assert.Equal(r, v.Reason);
    }

    [Fact]
    public void RecordVerification_Reason_501CharsRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        var r = new string('a', 501);
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, null, r, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Reason_ControlCharsRejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, null, "Text\nText", _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_EmptyVerificationId_Rejected()
    {
        var id = new HumanFactVerificationId(Guid.Empty);
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_UndefinedDecision_Rejected()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, (FactVerificationDecision)99, null, null, _verifyingActorId, _verifiedAt, _authority));
    }
}

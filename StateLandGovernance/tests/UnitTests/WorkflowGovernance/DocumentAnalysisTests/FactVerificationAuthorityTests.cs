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

public class FactVerificationAuthorityTests
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

    public FactVerificationAuthorityTests()
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
    public void RecordVerification_ExpiredSnapshot_ThrowsMissingVerifiedAuthorityException()
    {
        var expiredAuth = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "FactVerifier" }, _authority.Scope, _createdAt.AddDays(-2), _createdAt.AddDays(-2), _createdAt.AddDays(-1));
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, expiredAuth));
    }

    [Fact]
    public void RecordVerification_MissingCapability_ThrowsMissingVerifiedAuthorityException()
    {
        var noCapAuth = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "OtherRole" }, _authority.Scope, _authority.ValidFrom, _authority.VerificationTime, _authority.ValidUntil);
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, noCapAuth));
    }

    [Fact]
    public void RecordVerification_GlobalScope_Accepted()
    {
        var globalAuth = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "FactVerifier" }, new AuthorityScope(AuthorityScopeKind.Global, null), _authority.ValidFrom, _authority.VerificationTime, _authority.ValidUntil);
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, globalAuth);
        Assert.Single(_analysis.Verifications);
    }

    [Fact]
    public void RecordVerification_WrongGovernedDocumentTarget_ThrowsMissingVerifiedAuthorityException()
    {
        var wrongAuth = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "FactVerifier" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, Guid.NewGuid().ToString("D")), _authority.ValidFrom, _authority.VerificationTime, _authority.ValidUntil);
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, wrongAuth));
    }

    [Fact]
    public void RecordVerification_UnrelatedScope_ThrowsMissingVerifiedAuthorityException()
    {
        var leaseAuth = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "FactVerifier" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, "123"), _authority.ValidFrom, _authority.VerificationTime, _authority.ValidUntil);
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, leaseAuth));
    }

    [Fact]
    public void RecordVerification_ActionBeforeValidFrom_ThrowsInvalidFactVerificationException()
    {
        var auth = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "FactVerifier" }, _authority.Scope, _verifiedAt.AddDays(1), _verifiedAt.AddDays(2), _verifiedAt.AddDays(3));
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, auth));
    }

    [Fact]
    public void RecordVerification_ActionAfterValidUntil_ThrowsMissingVerifiedAuthorityException()
    {
        var auth = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "FactVerifier" }, _authority.Scope, _verifiedAt.AddDays(-2), _verifiedAt.AddDays(-2), _verifiedAt.AddDays(-1));
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, auth));
    }

    [Fact]
    public void RecordVerification_ActionBeforeVerificationTime_ThrowsInvalidFactVerificationException()
    {
        var auth = new VerifiedAuthoritySnapshot(_verifyingActorId, new[] { "FactVerifier" }, _authority.Scope, _authority.ValidFrom, _verifiedAt.AddMinutes(1), _authority.ValidUntil);
        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<InvalidFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, auth));
    }

    [Fact]
    public void RecordVerification_CopiedAuthorityEvidence_IsExact()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        var v = _analysis.Verifications.First();
        Assert.Equal(_verifyingActorId, v.VerifyingActorId);
        Assert.Equal("FactVerifier", v.VerifiedCapability);
        Assert.Equal(_authority.Scope.Kind, v.GrantedAuthorityScopeKind);
        Assert.Equal(_authority.Scope.TargetIdentifier, v.GrantedAuthorityScopeIdentifier);
        Assert.Equal(AuthorityScopeKind.GovernedDocument, v.RequiredAuthorityScopeKind);
        Assert.Equal(_analysis.GovernedDocumentId.Value.ToString("D"), v.RequiredAuthorityScopeIdentifier);
        Assert.Equal(_authority.ValidFrom, v.AuthorityValidFrom);
        Assert.Equal(_authority.VerificationTime, v.AuthorityVerificationTime);
        Assert.Equal(_authority.ValidUntil, v.AuthorityValidUntil);
    }

    [Fact]
    public void RecordVerification_FactExistsInAnotherResult_ThrowsExtractedFactNotFoundException()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        var res2Id = new AnalysisRunResultId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));
        var facts2 = new[] { new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("FC2"), new AnalysisFactValue(AnalysisFactValueKind.Text, "A"), null, null) };
        _analysis.CompleteRun(run2Id, res2Id, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), facts2, _runCompletedAt.AddMinutes(5));

        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<ExtractedFactNotFoundException>(() =>
            _analysis.RecordFactVerification(id, res2Id, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt.AddMinutes(10), _authority));
    }
}

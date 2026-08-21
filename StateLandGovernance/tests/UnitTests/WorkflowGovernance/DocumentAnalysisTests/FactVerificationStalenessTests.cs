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

public class FactVerificationStalenessTests
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

    public FactVerificationStalenessTests()
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
    public void RecordVerification_Staleness_NewerRequestedDoesNotStale()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));

        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        Assert.Single(_analysis.Verifications);
    }

    [Fact]
    public void RecordVerification_Staleness_NewerRunningDoesNotStale()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));

        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        Assert.Single(_analysis.Verifications);
    }

    [Fact]
    public void RecordVerification_Staleness_NewerFailedDoesNotStale()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));
        _analysis.FailRun(run2Id, new AnalysisRunFailure("Code", "Reason"), _runRequestedAt.AddMinutes(7));

        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        Assert.Single(_analysis.Verifications);
    }

    [Fact]
    public void RecordVerification_Staleness_NewerSupersededDoesNotStale()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));
        _analysis.SupersedeRun(run2Id, "Reason", _runRequestedAt.AddMinutes(7));

        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        Assert.Single(_analysis.Verifications);
    }

    [Fact]
    public void RecordVerification_Staleness_OlderCompletedRunDoesNotStaleNewerResult()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        var res2Id = new AnalysisRunResultId(Guid.NewGuid());
        var fact2Id = new ExtractedFactId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));
        var facts2 = new[] { new ExtractedFactInput(fact2Id, new FactCode("FC2"), new AnalysisFactValue(AnalysisFactValueKind.Text, "A"), null, null) };
        _analysis.CompleteRun(run2Id, res2Id, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), facts2, _runCompletedAt.AddMinutes(5));

        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, res2Id, fact2Id, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        Assert.Single(_analysis.Verifications);
    }

    [Fact]
    public void RecordVerification_Staleness_IdenticalCompletedAtWithHigherRunNumberStales()
    {
        var run2Id = new AnalysisRunId(Guid.NewGuid());
        var res2Id = new AnalysisRunResultId(Guid.NewGuid());
        _analysis.RequestRun(run2Id, new AnalysisModelReference("P", "M", "V"), new[] { new AnalysisCapabilityCode("C") }, _runRequestedAt.AddMinutes(5));
        _analysis.StartRun(run2Id, _runRequestedAt.AddMinutes(6));
        var facts2 = new[] { new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("FC2"), new AnalysisFactValue(AnalysisFactValueKind.Text, "A"), null, null) };
        _analysis.CompleteRun(run2Id, res2Id, AnalysisResultOutcome.OutputsProduced, Array.Empty<AnalysisResultArtifactReference>(), facts2, _runCompletedAt); // Same CompletedAt

        var id = new HumanFactVerificationId(Guid.NewGuid());
        Assert.Throws<StaleAnalysisResultException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority));
    }

    [Fact]
    public void RecordVerification_Duplicate_SameIdAndChangedDecisionThrowsConflicting()
    {
        var id = new HumanFactVerificationId(Guid.NewGuid());
        _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Confirmed, null, null, _verifyingActorId, _verifiedAt, _authority);
        
        Assert.Throws<ConflictingFactVerificationException>(() =>
            _analysis.RecordFactVerification(id, _resultId, _factId, FactVerificationDecision.Unsupported, null, "Reason", _verifyingActorId, _verifiedAt, _authority));
    }
}

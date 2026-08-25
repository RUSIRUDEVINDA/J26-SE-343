namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentAnalysisTests;

using System;
using System.Linq;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public class VerifiedFactSnapshotAuthorityTests
{
    private readonly DocumentAnalysis _analysis;
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly DateTime _utcNow = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly AnalysisRunId _runId = new AnalysisRunId(Guid.NewGuid());
    private readonly AnalysisRunResultId _resultId = new AnalysisRunResultId(Guid.NewGuid());

    public VerifiedFactSnapshotAuthorityTests()
    {
        _analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            new GovernedDocumentId(Guid.NewGuid()),
            new DocumentVersionId(Guid.NewGuid()),
            new DocumentChecksum("SHA256", "hash"),
            1, 1, _utcNow.AddDays(-10));

        _analysis.RequestRun(_runId, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-9));
        _analysis.StartRun(_runId, _utcNow.AddDays(-8));
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow.AddDays(-7));
    }

    private VerifiedAuthoritySnapshot CreateValidAuthority() =>
        new VerifiedAuthoritySnapshot(_actorId, new[] { "FactSnapshotPublisher" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, _analysis.GovernedDocumentId.Value.ToString("D")), _utcNow.AddDays(-20), _utcNow.AddDays(-10), _utcNow.AddDays(20));

    [Fact]
    public void PublishSnapshot_WrongPublishingActor_Throws()
    {
        var auth = CreateValidAuthority();
        Assert.Throws<MissingVerifiedAuthorityException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, Guid.NewGuid(), _utcNow, auth));
    }

    [Fact]
    public void PublishSnapshot_MissingCapability_Throws()
    {
        var auth = new VerifiedAuthoritySnapshot(_actorId, new[] { "Other" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, _analysis.GovernedDocumentId.Value.ToString("D")), _utcNow.AddDays(-20), _utcNow.AddDays(-10), _utcNow.AddDays(20));
        Assert.Throws<MissingVerifiedAuthorityException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, auth));
    }

    [Fact]
    public void PublishSnapshot_WrongScopeKind_Throws()
    {
        var auth = new VerifiedAuthoritySnapshot(_actorId, new[] { "FactSnapshotPublisher" }, new AuthorityScope(AuthorityScopeKind.LeaseCase, Guid.NewGuid().ToString("D")), _utcNow.AddDays(-20), _utcNow.AddDays(-10), _utcNow.AddDays(20));
        Assert.Throws<MissingVerifiedAuthorityException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, auth));
    }

    [Fact]
    public void PublishSnapshot_WrongScopeIdentifier_Throws()
    {
        var auth = new VerifiedAuthoritySnapshot(_actorId, new[] { "FactSnapshotPublisher" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, Guid.NewGuid().ToString("D")), _utcNow.AddDays(-20), _utcNow.AddDays(-10), _utcNow.AddDays(20));
        Assert.Throws<MissingVerifiedAuthorityException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, auth));
    }

    [Fact]
    public void PublishSnapshot_ExpiredAuthority_Throws()
    {
        var auth = new VerifiedAuthoritySnapshot(_actorId, new[] { "FactSnapshotPublisher" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, _analysis.GovernedDocumentId.Value.ToString("D")), _utcNow.AddDays(-20), _utcNow.AddDays(-10), _utcNow.AddDays(-1));
        Assert.Throws<MissingVerifiedAuthorityException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, auth));
    }

    [Fact]
    public void PublishSnapshot_BeforeAuthorityVerification_Throws()
    {
        var auth = new VerifiedAuthoritySnapshot(
            _actorId, 
            new[] { "FactSnapshotPublisher" }, 
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, _analysis.GovernedDocumentId.Value.ToString("D")), 
            _utcNow.AddDays(-10), 
            _utcNow.AddDays(-5), 
            _utcNow.AddDays(10));
            
        Assert.Throws<InvalidVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow.AddDays(-6), auth));
    }

    [Fact]
    public void PublishSnapshot_NullAuthority_Throws()
    {
        Assert.Throws<MissingVerifiedAuthorityException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, null!));
    }

    [Fact]
    public void PublishSnapshot_EmptyActor_Throws()
    {
        var auth = CreateValidAuthority();
        Assert.Throws<InvalidVerifiedFactSnapshotException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, Guid.Empty, _utcNow, auth));
    }

    [Fact]
    public void PublishSnapshot_CopiesExactAuthorityEvidence()
    {
        var auth = CreateValidAuthority();
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, auth);
        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        Assert.Equal(auth.Scope.Kind, snapshot.GrantedAuthorityScopeKind);
        Assert.Equal(auth.Scope.TargetIdentifier, snapshot.GrantedAuthorityScopeIdentifier);
        Assert.Equal(AuthorityScopeKind.GovernedDocument, snapshot.RequiredAuthorityScopeKind);
        Assert.Equal(_analysis.GovernedDocumentId.Value.ToString("D"), snapshot.RequiredAuthorityScopeIdentifier);
    }

    [Fact]
    public void PublishSnapshot_AuthorityObjectNotRetained()
    {
        var auth = CreateValidAuthority();
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, auth);
        var snapshot = _analysis.VerifiedFactSnapshots.Single();
        var props = snapshot.GetType().GetProperties();
        Assert.DoesNotContain(props, p => p.PropertyType == typeof(VerifiedAuthoritySnapshot));
    }
}

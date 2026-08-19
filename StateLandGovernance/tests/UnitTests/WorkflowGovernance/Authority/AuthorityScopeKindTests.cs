using System;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.Authority;

public class AuthorityScopeKindTests
{
    [Fact]
    public void AuthorityScopeKind_ContainsExpectedValues_InExactOrder()
    {
        var values = (AuthorityScopeKind[])Enum.GetValues(typeof(AuthorityScopeKind));
        Assert.Equal(6, values.Length);
        
        Assert.Equal(0, (int)values[0]);
        Assert.Equal("Global", values[0].ToString());
        
        Assert.Equal(1, (int)values[1]);
        Assert.Equal("LeaseCase", values[1].ToString());
        
        Assert.Equal(2, (int)values[2]);
        Assert.Equal("Agency", values[2].ToString());
        
        Assert.Equal(3, (int)values[3]);
        Assert.Equal("WorkflowExecution", values[3].ToString());
        
        Assert.Equal(4, (int)values[4]);
        Assert.Equal("ExecutionStage", values[4].ToString());
        
        Assert.Equal(5, (int)values[5]);
        Assert.Equal("GovernedDocument", values[5].ToString());
    }

    [Fact]
    public void AuthorityScope_GovernedDocument_WithoutTarget_ThrowsInvalidSnapshot()
    {
        Assert.Throws<StateLandGovernance.WorkflowGovernance.Domain.Exceptions.InvalidAuthoritySnapshotException>(() =>
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, null));
    }

    [Fact]
    public void AuthorityScope_GovernedDocument_WithWhitespaceTarget_ThrowsInvalidSnapshot()
    {
        Assert.Throws<StateLandGovernance.WorkflowGovernance.Domain.Exceptions.InvalidAuthoritySnapshotException>(() =>
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, "   "));
    }

    [Fact]
    public void Covers_Global_CoversGovernedDocument()
    {
        var granted = new AuthorityScope(AuthorityScopeKind.Global, null);
        var required = new AuthorityScope(AuthorityScopeKind.GovernedDocument, Guid.NewGuid().ToString());
        
        Assert.True(granted.Covers(required));
    }

    [Fact]
    public void Covers_IdenticalGovernedDocument_CoversIt()
    {
        var id = Guid.NewGuid().ToString();
        var granted = new AuthorityScope(AuthorityScopeKind.GovernedDocument, id);
        var required = new AuthorityScope(AuthorityScopeKind.GovernedDocument, id);
        
        Assert.True(granted.Covers(required));
    }

    [Fact]
    public void Covers_DifferentGovernedDocument_DoesNotCoverIt()
    {
        var granted = new AuthorityScope(AuthorityScopeKind.GovernedDocument, Guid.NewGuid().ToString());
        var required = new AuthorityScope(AuthorityScopeKind.GovernedDocument, Guid.NewGuid().ToString());
        
        Assert.False(granted.Covers(required));
    }

    [Fact]
    public void Covers_UnrelatedScopes_DoNotCoverGovernedDocument()
    {
        var required = new AuthorityScope(AuthorityScopeKind.GovernedDocument, Guid.NewGuid().ToString());
        
        var lease = new AuthorityScope(AuthorityScopeKind.LeaseCase, "target");
        var agency = new AuthorityScope(AuthorityScopeKind.Agency, "target");
        var workflow = new AuthorityScope(AuthorityScopeKind.WorkflowExecution, "target");
        var stage = new AuthorityScope(AuthorityScopeKind.ExecutionStage, "target");
        
        Assert.False(lease.Covers(required));
        Assert.False(agency.Covers(required));
        Assert.False(workflow.Covers(required));
        Assert.False(stage.Covers(required));
    }
}

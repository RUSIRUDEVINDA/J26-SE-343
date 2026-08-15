using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.Authority;

public class VerifiedAuthoritySnapshotTests
{
    private readonly Guid _validActorId = Guid.NewGuid();
    private readonly string[] _validCapabilities = new[] { "TestCapability" };
    private readonly AuthorityScope _validScope = new AuthorityScope(AuthorityScopeKind.Global, null);
    private readonly DateTime _validFrom = DateTime.UtcNow.AddMinutes(-10);
    private readonly DateTime _verificationTime = DateTime.UtcNow.AddMinutes(-5);
    private readonly DateTime _validUntil = DateTime.UtcNow.AddMinutes(10);

    [Fact]
    public void Create_ValidAuthoritySnapshot_PreservesValues()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);

        Assert.Equal(_validActorId, snapshot.ActorId);
        Assert.Single(snapshot.Capabilities);
        Assert.Contains("TestCapability", snapshot.Capabilities);
        Assert.Equal(_validScope, snapshot.Scope);
        Assert.Equal(_validFrom, snapshot.ValidFrom);
        Assert.Equal(_verificationTime, snapshot.VerificationTime);
        Assert.Equal(_validUntil, snapshot.ValidUntil);
    }

    [Fact]
    public void Create_EmptyActorId_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(Guid.Empty, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil));
    }

    [Fact]
    public void Create_NullCapabilities_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(_validActorId, null!, _validScope, _validFrom, _verificationTime, _validUntil));
    }

    [Fact]
    public void Create_EmptyCapabilities_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(_validActorId, Array.Empty<string>(), _validScope, _validFrom, _verificationTime, _validUntil));
    }

    [Fact]
    public void Create_BlankCapability_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(_validActorId, new[] { " " }, _validScope, _validFrom, _verificationTime, _validUntil));
    }

    [Fact]
    public void Create_DuplicateCapability_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(_validActorId, new[] { "CapA", "CapA" }, _validScope, _validFrom, _verificationTime, _validUntil));
    }
    
    [Fact]
    public void Create_NullScope_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, null!, _validFrom, _verificationTime, _validUntil));
    }

    [Fact]
    public void Create_NonUtcTimestamps_ThrowsInvalidSnapshot()
    {
        var localTime = DateTime.Now;
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, localTime, _verificationTime, _validUntil));
    }

    [Fact]
    public void EnsureAuthorizes_NonUtcActionTime_ThrowsMissingAuthority()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        var localTime = DateTime.Now;
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            snapshot.EnsureAuthorizes(_validActorId, "TestCapability", _validScope, localTime));
    }

    [Fact]
    public void Create_VerificationBeforeValidFrom_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _validFrom.AddMinutes(-1), _validUntil));
    }

    [Fact]
    public void Create_VerificationAfterValidUntil_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _validUntil.AddMinutes(1), _validUntil));
    }

    [Fact]
    public void Create_MutableCapabilitySource_DefensivelyCopiesCapabilities()
    {
        var list = new List<string> { "CapA" };
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, list, _validScope, _validFrom, _verificationTime, _validUntil);
        
        list.Add("CapB");
        
        Assert.Single(snapshot.Capabilities);
        Assert.Contains("CapA", snapshot.Capabilities);
    }
    
    [Fact]
    public void ExposedCapabilities_CannotBeCastAndMutated()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        
        var capsList = snapshot.Capabilities as IList<string>;
        Assert.NotNull(capsList);
        Assert.Throws<NotSupportedException>(() => capsList.Add("MaliciousCapability"));
        
        var stringArray = snapshot.Capabilities as string[];
        Assert.Null(stringArray);
    }

    [Fact]
    public void EnsureAuthorizes_MatchingActorCapabilityScopeAndTime_Succeeds()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        
        snapshot.EnsureAuthorizes(_validActorId, "TestCapability", _validScope, _verificationTime.AddMinutes(1));
    }

    [Fact]
    public void EnsureAuthorizes_MismatchedActor_ThrowsMissingAuthority()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            snapshot.EnsureAuthorizes(Guid.NewGuid(), "TestCapability", _validScope, _verificationTime.AddMinutes(1)));
    }

    [Fact]
    public void EnsureAuthorizes_MissingCapability_ThrowsMissingAuthority()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            snapshot.EnsureAuthorizes(_validActorId, "OtherCapability", _validScope, _verificationTime.AddMinutes(1)));
    }

    [Fact]
    public void EnsureAuthorizes_MismatchedScope_ThrowsMissingAuthority()
    {
        var specificScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, "Case123");
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, specificScope, _validFrom, _verificationTime, _validUntil);
        
        var otherScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, "Case456");
        
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            snapshot.EnsureAuthorizes(_validActorId, "TestCapability", otherScope, _verificationTime.AddMinutes(1)));
    }

    [Fact]
    public void EnsureAuthorizes_GlobalScope_CoversSpecificScope()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        var specificScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, "Case123");
        
        snapshot.EnsureAuthorizes(_validActorId, "TestCapability", specificScope, _verificationTime.AddMinutes(1));
    }

    [Fact]
    public void EnsureAuthorizes_ActionBeforeValidFrom_ThrowsMissingAuthority()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            snapshot.EnsureAuthorizes(_validActorId, "TestCapability", _validScope, _validFrom.AddMinutes(-1)));
    }

    [Fact]
    public void EnsureAuthorizes_ActionAfterValidUntil_ThrowsMissingAuthority()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            snapshot.EnsureAuthorizes(_validActorId, "TestCapability", _validScope, _validUntil.AddMinutes(1)));
    }

    [Fact]
    public void EnsureAuthorizes_ActionAtValidityBoundaries_Succeeds()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        
        snapshot.EnsureAuthorizes(_validActorId, "TestCapability", _validScope, _validFrom);
        snapshot.EnsureAuthorizes(_validActorId, "TestCapability", _validScope, _validUntil);
    }

    [Fact]
    public void EnsureAuthorizes_DifferentlyCasedCapability_ThrowsMissingAuthority()
    {
        var snapshot = new VerifiedAuthoritySnapshot(_validActorId, _validCapabilities, _validScope, _validFrom, _verificationTime, _validUntil);
        
        Assert.Throws<MissingVerifiedAuthorityException>(() =>
            snapshot.EnsureAuthorizes(_validActorId, "testcapability", _validScope, _verificationTime.AddMinutes(1)));
    }

    [Fact]
    public void AuthorityScope_GlobalWithTarget_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new AuthorityScope(AuthorityScopeKind.Global, "Target"));
    }

    [Fact]
    public void AuthorityScope_NonGlobalWithoutTarget_ThrowsInvalidSnapshot()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() =>
            new AuthorityScope(AuthorityScopeKind.LeaseCase, null));
    }
}

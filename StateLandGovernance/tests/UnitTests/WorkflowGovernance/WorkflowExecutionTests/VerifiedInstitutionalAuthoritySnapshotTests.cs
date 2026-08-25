namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowExecutionTests;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class VerifiedInstitutionalAuthoritySnapshotTests
{
    private readonly Guid Actor = Guid.NewGuid();
    private InstitutionCode Inst => new InstitutionCode("I");
    private AuthorityScope Scope => new AuthorityScope(AuthorityScopeKind.LeaseCase, "L");
    private readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Constructor_Valid_CreatesSnapshot()
    {
        var s = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
        Assert.Equal(Actor, s.ActorId);
        Assert.Equal("I", s.InstitutionCode.Value);
        Assert.Single(s.Capabilities);
    }

    [Fact]
    public void Constructor_EmptyActor_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Guid.Empty, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_DefaultInstitution_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, default, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_NullScope_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, null!, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_NullCapabilities_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, null!, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_EmptyCapabilities_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, Array.Empty<string>(), Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_BlankCapability_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { " " }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_DuplicateCapability_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C", "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_ControlCharacters_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C\x07" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_NonUtc_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, DateTime.Now.AddMinutes(-5), Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_ValidFromAfterValidUntil_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(5), Now, Now.AddMinutes(-5)));
    }

    [Fact]
    public void Constructor_VerificationTimeBeforeValidFrom_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now.AddMinutes(-10), Now.AddMinutes(5)));
    }

    [Fact]
    public void Constructor_VerificationTimeAfterValidUntil_Throws()
    {
        Assert.Throws<InvalidAuthoritySnapshotException>(() => new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now.AddMinutes(10), Now.AddMinutes(5)));
    }
    
    [Fact]
    public void EnsureAuthorizes_WrongActor_Throws()
    {
        var s = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
        Assert.Throws<MissingVerifiedAuthorityException>(() => s.EnsureAuthorizes(Guid.NewGuid(), Inst, "C", Scope, Now));
    }
    
    [Fact]
    public void EnsureAuthorizes_WrongInstitution_Throws()
    {
        var s = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
        Assert.Throws<MissingVerifiedAuthorityException>(() => s.EnsureAuthorizes(Actor, new InstitutionCode("OTHER"), "C", Scope, Now));
    }

    [Fact]
    public void EnsureAuthorizes_WrongCapability_Throws()
    {
        var s = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
        Assert.Throws<MissingVerifiedAuthorityException>(() => s.EnsureAuthorizes(Actor, Inst, "OTHER", Scope, Now));
    }
    
    [Fact]
    public void EnsureAuthorizes_WrongScope_Throws()
    {
        var s = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
        Assert.Throws<MissingVerifiedAuthorityException>(() => s.EnsureAuthorizes(Actor, Inst, "C", new AuthorityScope(AuthorityScopeKind.LeaseCase, "OTHER"), Now));
    }
    
    [Fact]
    public void EnsureAuthorizes_BeforeValidFrom_Throws()
    {
        var s = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
        Assert.Throws<MissingVerifiedAuthorityException>(() => s.EnsureAuthorizes(Actor, Inst, "C", Scope, Now.AddMinutes(-10)));
    }
    
    [Fact]
    public void EnsureAuthorizes_AfterValidUntil_Throws()
    {
        var s = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
        Assert.Throws<MissingVerifiedAuthorityException>(() => s.EnsureAuthorizes(Actor, Inst, "C", Scope, Now.AddMinutes(10)));
    }
    
    [Fact]
    public void EnsureAuthorizes_BeforeVerificationTime_Throws()
    {
        var s = new VerifiedInstitutionalAuthoritySnapshot(Actor, Inst, new[] { "C" }, Scope, Now.AddMinutes(-5), Now, Now.AddMinutes(5));
        Assert.Throws<MissingVerifiedAuthorityException>(() => s.EnsureAuthorizes(Actor, Inst, "C", Scope, Now.AddMinutes(-2)));
    }
}




using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.Events;
using Xunit;

namespace StateLandGovernance.UnitTests.WorkflowGovernance.Leases;

public class LeaseCaseTests
{
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly LeaseCaseId _validLeaseCaseId = new LeaseCaseId(Guid.NewGuid());
    private readonly string _validAppRef = "APP-123";
    private readonly DateTime _actionTime = DateTime.UtcNow;
    private readonly VerifiedAuthoritySnapshot _validAuthority;
    private readonly VerifiedAuthoritySnapshot _globalAuthority;

    public LeaseCaseTests()
    {
        var validFrom = _actionTime.AddMinutes(-5);
        var verificationTime = _actionTime.AddMinutes(-2);
        var validUntil = _actionTime.AddMinutes(10);
        
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, _validLeaseCaseId.Value.ToString());
        _validAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "LeaseInitiator" }, scope, validFrom, verificationTime, validUntil);

        var globalScope = new AuthorityScope(AuthorityScopeKind.Global, null);
        _globalAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "LeaseInitiator" }, globalScope, validFrom, verificationTime, validUntil);
    }

    [Fact]
    public void Create_ValidInputs_InitializesLeaseCase()
    {
        var leaseCase = new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, _actionTime, _validAuthority);

        Assert.Equal(_validLeaseCaseId, leaseCase.Id);
        Assert.Equal(_validAppRef, leaseCase.ApplicationReference);
        Assert.Equal(_actorId, leaseCase.CreatedByActorId);
        Assert.Equal(_actionTime, leaseCase.CreatedAt);
        Assert.Equal(1, leaseCase.Revision);
    }

    [Fact]
    public void Create_EmptyLeaseCaseId_ThrowsInvalidLeaseCase()
    {
        Assert.Throws<InvalidLeaseCaseException>(() => new LeaseCaseId(Guid.Empty));
        Assert.Throws<InvalidLeaseCaseException>(() => new LeaseCase(default, _validAppRef, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Create_NullApplicationReference_ThrowsInvalidLeaseCase()
    {
        Assert.Throws<InvalidLeaseCaseException>(() => new LeaseCase(_validLeaseCaseId, null!, _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Create_EmptyApplicationReference_ThrowsInvalidLeaseCase()
    {
        Assert.Throws<InvalidLeaseCaseException>(() => new LeaseCase(_validLeaseCaseId, "", _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Create_WhitespaceApplicationReference_ThrowsInvalidLeaseCase()
    {
        Assert.Throws<InvalidLeaseCaseException>(() => new LeaseCase(_validLeaseCaseId, "   ", _actorId, _actionTime, _validAuthority));
    }

    [Fact]
    public void Create_EmptyActorId_ThrowsInvalidLeaseCase()
    {
        Assert.Throws<InvalidLeaseCaseException>(() => new LeaseCase(_validLeaseCaseId, _validAppRef, Guid.Empty, _actionTime, _validAuthority));
    }

    [Fact]
    public void Create_MismatchedAuthorityActor_ThrowsMissingAuthority()
    {
        Assert.Throws<MissingVerifiedAuthorityException>(() => new LeaseCase(_validLeaseCaseId, _validAppRef, Guid.NewGuid(), _actionTime, _validAuthority));
    }

    [Fact]
    public void Create_MissingLeaseInitiatorCapability_ThrowsMissingAuthority()
    {
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, _validLeaseCaseId.Value.ToString());
        var invalidAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "OtherCapability" }, scope, _actionTime.AddMinutes(-5), _actionTime.AddMinutes(-2), _actionTime.AddMinutes(10));
        
        Assert.Throws<MissingVerifiedAuthorityException>(() => new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, _actionTime, invalidAuthority));
    }

    [Fact]
    public void Create_MismatchedLeaseCaseScope_ThrowsMissingAuthority()
    {
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, Guid.NewGuid().ToString());
        var mismatchedScopeAuthority = new VerifiedAuthoritySnapshot(_actorId, new[] { "LeaseInitiator" }, scope, _actionTime.AddMinutes(-5), _actionTime.AddMinutes(-2), _actionTime.AddMinutes(10));
        
        Assert.Throws<MissingVerifiedAuthorityException>(() => new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, _actionTime, mismatchedScopeAuthority));
    }

    [Fact]
    public void Create_GlobalAuthorityScope_Succeeds()
    {
        var leaseCase = new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, _actionTime, _globalAuthority);
        Assert.NotNull(leaseCase);
    }

    [Fact]
    public void Create_ActionBeforeAuthorityValidity_ThrowsMissingAuthority()
    {
        var invalidTime = _actionTime.AddMinutes(-10);
        Assert.Throws<MissingVerifiedAuthorityException>(() => new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, invalidTime, _validAuthority));
    }

    [Fact]
    public void Create_ActionAfterAuthorityValidity_ThrowsMissingAuthority()
    {
        var invalidTime = _actionTime.AddMinutes(20);
        Assert.Throws<MissingVerifiedAuthorityException>(() => new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, invalidTime, _validAuthority));
    }

    [Fact]
    public void Create_NonUtcActionTime_ThrowsExpectedDomainFailure()
    {
        var localTime = DateTime.Now;
        Assert.Throws<MissingVerifiedAuthorityException>(() => new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, localTime, _validAuthority));
    }

    [Fact]
    public void Create_Success_RaisesOneLeaseCaseInitializedEvent()
    {
        var leaseCase = new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, _actionTime, _validAuthority);
        Assert.Single(leaseCase.DomainEvents);
    }

    [Fact]
    public void Create_EventContainsExpectedIdentifiersTimeAndRevision()
    {
        var leaseCase = new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, _actionTime, _validAuthority);
        var domainEvent = leaseCase.DomainEvents.First() as LeaseCaseInitialized;
        
        Assert.NotNull(domainEvent);
        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
        Assert.Equal(_actionTime, domainEvent.OccurredOn);
        Assert.Equal(_validLeaseCaseId, domainEvent.LeaseCaseId);
        Assert.Equal(_validAppRef, domainEvent.ApplicationReference);
        Assert.Equal(_actorId, domainEvent.ActorId);
        Assert.Equal(1, domainEvent.LeaseCaseRevision);
    }

    [Fact]
    public void Create_Failure_DoesNotProduceLeaseCaseOrEvent()
    {
        LeaseCase? failedCase = null;
        try
        {
            failedCase = new LeaseCase(_validLeaseCaseId, "", _actorId, _actionTime, _validAuthority);
        }
        catch (InvalidLeaseCaseException) { }

        Assert.Null(failedCase);
    }

    [Fact]
    public void DomainEvents_ExposedCollection_CannotBeMutated()
    {
        var leaseCase = new LeaseCase(_validLeaseCaseId, _validAppRef, _actorId, _actionTime, _validAuthority);
        var eventsList = leaseCase.DomainEvents as IList<StateLandGovernance.BuildingBlocks.Events.IDomainEvent>;
        
        Assert.NotNull(eventsList);
        Assert.Throws<NotSupportedException>(() => eventsList.Add(new LeaseCaseInitialized(Guid.NewGuid(), _actionTime, _validLeaseCaseId, "test", _actorId, 1)));
    }

    [Fact]
    public void LeaseCaseId_SameGuid_ValuesAreEqual()
    {
        var guid = Guid.NewGuid();
        var id1 = new LeaseCaseId(guid);
        var id2 = new LeaseCaseId(guid);

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
    }

    [Fact]
    public void LeaseCaseId_DifferentGuid_ValuesAreNotEqual()
    {
        var id1 = new LeaseCaseId(Guid.NewGuid());
        var id2 = new LeaseCaseId(Guid.NewGuid());

        Assert.NotEqual(id1, id2);
        Assert.True(id1 != id2);
    }
}

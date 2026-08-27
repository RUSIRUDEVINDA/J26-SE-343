namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.Events;

public sealed class LeaseCase
{
    public LeaseCaseId Id { get; }
    public string ApplicationReference { get; }
    public Guid CreatedByActorId { get; }
    public DateTime CreatedAt { get; }
    public int Revision { get; }
    
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public LeaseCase(
        LeaseCaseId id,
        string applicationReference,
        Guid actorId,
        DateTime actionTime,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (id == default || id.Value == Guid.Empty)
        {
            throw new InvalidLeaseCaseException("LeaseCaseId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(applicationReference))
        {
            throw new InvalidLeaseCaseException("ApplicationReference cannot be null, empty, or whitespace.");
        }

        if (actorId == Guid.Empty)
        {
            throw new InvalidLeaseCaseException("ActorId cannot be empty.");
        }

        if (authoritySnapshot == null)
        {
            throw new InvalidLeaseCaseException("VerifiedAuthoritySnapshot is required.");
        }

        var requiredScope = new AuthorityScope(AuthorityScopeKind.LeaseCase, id.Value.ToString());
        
        authoritySnapshot.EnsureAuthorizes(actorId, "LeaseInitiator", requiredScope, actionTime);

        Id = id;
        ApplicationReference = applicationReference;
        CreatedByActorId = actorId;
        CreatedAt = actionTime;
        Revision = 1;

        _domainEvents.Add(new LeaseCaseInitialized(
            Guid.NewGuid(),
            actionTime,
            id,
            applicationReference,
            actorId,
            Revision
        ));
    }
}

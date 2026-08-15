namespace StateLandGovernance.WorkflowGovernance.Domain.Authority;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class VerifiedAuthoritySnapshot
{
    public Guid ActorId { get; }
    private readonly string[] _capabilities;
    public IReadOnlyCollection<string> Capabilities => Array.AsReadOnly(_capabilities);
    public AuthorityScope Scope { get; }
    public DateTime ValidFrom { get; }
    public DateTime VerificationTime { get; }
    public DateTime ValidUntil { get; }

    public VerifiedAuthoritySnapshot(
        Guid actorId,
        IEnumerable<string> capabilities,
        AuthorityScope scope,
        DateTime validFrom,
        DateTime verificationTime,
        DateTime validUntil)
    {
        if (actorId == Guid.Empty)
        {
            throw new InvalidAuthoritySnapshotException("ActorId cannot be empty.");
        }

        if (capabilities == null)
        {
            throw new InvalidAuthoritySnapshotException("Capabilities cannot be null or empty.");
        }

        var capsArray = capabilities.ToArray();

        if (capsArray.Length == 0)
        {
            throw new InvalidAuthoritySnapshotException("Capabilities cannot be null or empty.");
        }

        foreach (var cap in capsArray)
        {
            if (string.IsNullOrWhiteSpace(cap))
            {
                throw new InvalidAuthoritySnapshotException("Capability identifiers cannot be null, empty or whitespace.");
            }
        }

        var distinctCaps = capsArray.Distinct(StringComparer.Ordinal).ToArray();
        if (distinctCaps.Length != capsArray.Length)
        {
            throw new InvalidAuthoritySnapshotException("Duplicate capability identifiers are invalid.");
        }

        if (scope == null)
        {
            throw new InvalidAuthoritySnapshotException("AuthorityScope is required.");
        }

        if (validFrom.Kind != DateTimeKind.Utc || verificationTime.Kind != DateTimeKind.Utc || validUntil.Kind != DateTimeKind.Utc)
        {
            throw new InvalidAuthoritySnapshotException("Timestamps must be UTC.");
        }

        if (validFrom > verificationTime || verificationTime > validUntil)
        {
            throw new InvalidAuthoritySnapshotException("Validity period invariants violated.");
        }

        ActorId = actorId;
        _capabilities = distinctCaps;
        Scope = scope;
        ValidFrom = validFrom;
        VerificationTime = verificationTime;
        ValidUntil = validUntil;
    }

    public void EnsureAuthorizes(Guid actorId, string requiredCapability, AuthorityScope requiredScope, DateTime actionTime)
    {
        if (actionTime.Kind != DateTimeKind.Utc)
        {
            throw new MissingVerifiedAuthorityException("Action time must be UTC.");
        }

        if (ActorId != actorId)
        {
            throw new MissingVerifiedAuthorityException("ActorId does not match snapshot.");
        }

        if (actionTime < ValidFrom || actionTime > ValidUntil)
        {
            throw new MissingVerifiedAuthorityException("Action time is outside validity period.");
        }

        if (string.IsNullOrWhiteSpace(requiredCapability) || !_capabilities.Contains(requiredCapability, StringComparer.Ordinal))
        {
            throw new MissingVerifiedAuthorityException("Required capability is missing.");
        }

        if (requiredScope == null || !Scope.Covers(requiredScope))
        {
            throw new MissingVerifiedAuthorityException("Required scope is not covered.");
        }
    }
}

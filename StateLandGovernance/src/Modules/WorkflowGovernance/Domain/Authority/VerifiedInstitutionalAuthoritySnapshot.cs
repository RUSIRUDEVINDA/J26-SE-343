namespace StateLandGovernance.WorkflowGovernance.Domain.Authority;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed class VerifiedInstitutionalAuthoritySnapshot
{
    public Guid ActorId { get; }
    public InstitutionCode InstitutionCode { get; }
    private readonly string[] _capabilities;
    public IReadOnlyCollection<string> Capabilities => Array.AsReadOnly(_capabilities);
    public AuthorityScope Scope { get; }
    public DateTime ValidFrom { get; }
    public DateTime VerificationTime { get; }
    public DateTime ValidUntil { get; }

    public VerifiedInstitutionalAuthoritySnapshot(
        Guid actorId,
        InstitutionCode institutionCode,
        IEnumerable<string> capabilities,
        AuthorityScope scope,
        DateTime validFrom,
        DateTime verificationTime,
        DateTime validUntil)
    {
        if (actorId == Guid.Empty)
            throw new InvalidAuthoritySnapshotException("ActorId cannot be empty.");

        if (institutionCode == default)
            throw new InvalidAuthoritySnapshotException("InstitutionCode cannot be default.");

        if (capabilities == null)
            throw new InvalidAuthoritySnapshotException("Capabilities cannot be null or empty.");

        var capsArray = capabilities.ToArray();

        if (capsArray.Length == 0)
            throw new InvalidAuthoritySnapshotException("Capabilities cannot be null or empty.");

        var trimmedCaps = new List<string>();
        foreach (var cap in capsArray)
        {
            if (string.IsNullOrWhiteSpace(cap))
                throw new InvalidAuthoritySnapshotException("Capability identifiers cannot be null, empty or whitespace.");
            var trimmed = cap.Trim();
            if (trimmed.Length > 100)
                throw new InvalidAuthoritySnapshotException("Capability cannot exceed 100 characters.");
            if (trimmed.Any(char.IsControl))
                throw new InvalidAuthoritySnapshotException("Capability cannot contain control characters.");
            trimmedCaps.Add(trimmed);
        }

        var distinctCaps = trimmedCaps.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (distinctCaps.Length != trimmedCaps.Count)
            throw new InvalidAuthoritySnapshotException("Duplicate capability identifiers are invalid."); capsArray.Distinct(StringComparer.Ordinal).ToArray();
        if (distinctCaps.Length != capsArray.Length)
            throw new InvalidAuthoritySnapshotException("Duplicate capability identifiers are invalid.");

        if (scope == null)
            throw new InvalidAuthoritySnapshotException("AuthorityScope is required.");

        if (validFrom.Kind != DateTimeKind.Utc || verificationTime.Kind != DateTimeKind.Utc || validUntil.Kind != DateTimeKind.Utc)
            throw new InvalidAuthoritySnapshotException("Timestamps must be UTC.");

        if (validFrom > validUntil)
            throw new InvalidAuthoritySnapshotException("ValidFrom cannot be after ValidUntil.");

        if (verificationTime < validFrom || verificationTime > validUntil)
            throw new InvalidAuthoritySnapshotException("VerificationTime must be within the validity period.");

        ActorId = actorId;
        InstitutionCode = institutionCode;
        _capabilities = distinctCaps.ToArray();
        Scope = scope;
        ValidFrom = validFrom;
        VerificationTime = verificationTime;
        ValidUntil = validUntil;
    }

    public void EnsureAuthorizes(
        Guid expectedActorId,
        InstitutionCode expectedInstitution,
        string requiredCapability,
        AuthorityScope expectedScope,
        DateTime actionTime)
    {
        if (actionTime.Kind != DateTimeKind.Utc)
            throw new MissingVerifiedAuthorityException("Action time must be UTC.");

        if (ActorId != expectedActorId)
            throw new MissingVerifiedAuthorityException("ActorId does not match snapshot.");

        if (InstitutionCode.Value != expectedInstitution.Value)
            throw new MissingVerifiedAuthorityException("Institution does not match snapshot.");

        if (actionTime < ValidFrom || actionTime > ValidUntil)
            throw new MissingVerifiedAuthorityException("Action time is outside validity period.");
            
        if (actionTime < VerificationTime)
            throw new MissingVerifiedAuthorityException("Action time is before verification time.");

        if (string.IsNullOrWhiteSpace(requiredCapability) || !_capabilities.Contains(requiredCapability, StringComparer.Ordinal))
            throw new MissingVerifiedAuthorityException("Required capability is missing.");

        if (expectedScope == null || !Scope.Covers(expectedScope))
            throw new MissingVerifiedAuthorityException("Required scope is not covered.");
    }
}





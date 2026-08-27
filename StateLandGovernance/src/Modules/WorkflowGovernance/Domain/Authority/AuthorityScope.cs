namespace StateLandGovernance.WorkflowGovernance.Domain.Authority;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AuthorityScope
{
    public AuthorityScopeKind Kind { get; }
    public string? TargetIdentifier { get; }

    public AuthorityScope(AuthorityScopeKind kind, string? targetIdentifier)
    {
        if (kind == AuthorityScopeKind.Global)
        {
            if (!string.IsNullOrEmpty(targetIdentifier))
            {
                throw new InvalidAuthoritySnapshotException("Global scope must not contain a target identifier.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(targetIdentifier))
            {
                throw new InvalidAuthoritySnapshotException("Non-global scope must contain a nonempty target identifier.");
            }
        }

        Kind = kind;
        TargetIdentifier = targetIdentifier;
    }

    public bool Covers(AuthorityScope requiredScope)
    {
        if (requiredScope == null)
            throw new ArgumentNullException(nameof(requiredScope));

        if (Kind == AuthorityScopeKind.Global)
            return true;

        if (Kind != requiredScope.Kind)
            return false;

        return string.Equals(TargetIdentifier, requiredScope.TargetIdentifier, StringComparison.Ordinal);
    }
}

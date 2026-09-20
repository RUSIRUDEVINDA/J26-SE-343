namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;

/// <summary>
/// Application boundary representation of verified caller authority.
/// Protects the Presentation layer from needing to construct Domain authorization objects directly.
/// </summary>
public sealed record VerifiedAuthorityContext(
    Guid ActorId,
    IReadOnlyList<string> Capabilities,
    string ScopeKind,
    string? ScopeTargetIdentifier,
    DateTime ValidFrom,
    DateTime VerificationTime,
    DateTime ValidUntil)
{
    public static VerifiedAuthorityContext FromDomain(VerifiedAuthoritySnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        return new VerifiedAuthorityContext(
            snapshot.ActorId,
            new List<string>(snapshot.Capabilities),
            snapshot.Scope.Kind.ToString(),
            snapshot.Scope.TargetIdentifier,
            snapshot.ValidFrom,
            snapshot.VerificationTime,
            snapshot.ValidUntil);
    }

    public VerifiedAuthoritySnapshot ToDomain()
    {
        if (!Enum.TryParse<AuthorityScopeKind>(ScopeKind, ignoreCase: true, out var kind))
        {
            throw new ArgumentException($"Invalid authority scope kind: '{ScopeKind}'.", nameof(ScopeKind));
        }

        var scope = new AuthorityScope(kind, ScopeTargetIdentifier);
        return new VerifiedAuthoritySnapshot(
            ActorId,
            Capabilities,
            scope,
            ValidFrom,
            VerificationTime,
            ValidUntil);
    }
}

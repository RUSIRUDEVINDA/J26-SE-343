using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.Api.Security;

public sealed class DevelopmentRegulatoryReportReadOptions
{
    public bool Enabled { get; set; }
    public List<RegulatoryReadAssignment> Assignments { get; set; } = new();
    public List<LocalOperatorRegulatoryReadGrant> LocalOperatorGrants { get; set; } = new();
}
public sealed class RegulatoryReadAssignment
{
    public Guid AssignmentId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid ActorId { get; set; }
    public Guid AssignedByActorId { get; set; }
    public string SourceReference { get; set; } = "";
    public DateTimeOffset ValidFromUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
// Local operator authorization is explicitly distinct from an officer-issued assignment.
public sealed class LocalOperatorRegulatoryReadGrant
{
    public Guid GrantId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid ActorId { get; set; }
    public string AuthorizationSource { get; set; } = "";
    public DateTimeOffset ValidFromUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
// Configuration must be controlled by the server administrator; never populated from JWT/body/query/browser state.
// This is explicit Development-only assignment, not recovered ownership or a production assignment store.
public sealed class DevelopmentRegulatoryReportAssignments(IOptionsMonitor<DevelopmentRegulatoryReportReadOptions> options,
    IHostEnvironment environment, TimeProvider? time = null, IHttpContextAccessor? http = null) : IIndependentRegulatoryReadAssignments
{
    public Task<IReadOnlyList<Guid>> ReadAllowedIdsAsync(Guid actorId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var settings = options.CurrentValue;
        if (!environment.IsDevelopment() || !settings.Enabled || actorId == Guid.Empty)
            return Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
        var now = (time ?? TimeProvider.System).GetUtcNow();
        var ids = (settings.Assignments ?? new()).Where(x => x != null && x.AssignmentId != Guid.Empty &&
            x.EvaluationId != Guid.Empty && x.ActorId == actorId && x.AssignedByActorId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(x.SourceReference) && x.RevokedAtUtc == null &&
            x.ValidFromUtc <= now && x.ExpiresAtUtc > now && x.ExpiresAtUtc > x.ValidFromUtc)
            .Select(x => x.EvaluationId).Distinct().ToArray();
        var remote = http?.HttpContext?.Connection.RemoteIpAddress;
        var local = remote != null && System.Net.IPAddress.IsLoopback(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote);
        var operatorIds = local ? (settings.LocalOperatorGrants ?? new()).Where(x => x != null &&
            x.GrantId != Guid.Empty && x.EvaluationId != Guid.Empty && x.ActorId == actorId &&
            !string.IsNullOrWhiteSpace(x.AuthorizationSource) && x.RevokedAtUtc == null &&
            x.ValidFromUtc.Offset == TimeSpan.Zero && x.ExpiresAtUtc.Offset == TimeSpan.Zero &&
            x.ValidFromUtc <= now && x.ExpiresAtUtc > now && x.ExpiresAtUtc > x.ValidFromUtc &&
            x.ExpiresAtUtc - x.ValidFromUtc <= TimeSpan.FromHours(24))
            .Select(x => x.EvaluationId) : Enumerable.Empty<Guid>();
        return Task.FromResult<IReadOnlyList<Guid>>(ids.Concat(operatorIds).Distinct().ToArray());
    }
}

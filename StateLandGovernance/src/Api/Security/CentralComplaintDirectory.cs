using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.Api.Security;

public sealed class ComplaintHttpActor(IHttpContextAccessor http) : IComplaintActorContext
{
    public Guid ActorId => Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? http.HttpContext?.User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

// A host adapter: reads only accounts/settings, never C3 code or assignment writes.
public sealed class CentralComplaintDirectory(IConfiguration configuration) : IComplaintOfficerDirectory
{
    private async Task<(Dictionary<Guid, (string Role, string Institution)> Profiles, JsonElement Reviewers)> LoadAsync(CancellationToken ct)
    {
        try { return await LoadCoreAsync(ct); }
        catch (Exception e) when (e is Npgsql.NpgsqlException or JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new CentralComplaintUnavailableException(); }
    }
    private async Task<(Dictionary<Guid, (string Role, string Institution)> Profiles, JsonElement Reviewers)> LoadCoreAsync(CancellationToken ct)
    {
        await using var db = PortalAccountStore.Open(configuration);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Constant SQL establishes a stronger read-only boundary even if an operator supplied a broader account role.
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var profiles = new Dictionary<Guid, (string, string)>();
        foreach (var row in await db.Accounts.AsNoTracking().ToArrayAsync(ct))
        {
            using var json = JsonDocument.Parse(row.ProfileJson);
            var p = json.RootElement;
            if (!Guid.TryParse(p.GetProperty("ActorId").GetString(), out var actor) || actor != row.ActorId || profiles.ContainsKey(actor))
                throw new CentralComplaintUnavailableException();
            // Legacy profiles without a disable marker retain their existing status.
            // Explicit malformed/inactive markers fail closed for C4; no account is rewritten.
            if ((p.TryGetProperty("IsActive",out var active) && active.ValueKind!=JsonValueKind.True) ||
                (p.TryGetProperty("Disabled",out var disabled) && disabled.ValueKind!=JsonValueKind.False)) continue;
            profiles.Add(actor, (p.GetProperty("TokenRole").GetString() ?? "", p.GetProperty("Institution").GetString() ?? ""));
        }
        var setting = await db.Settings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == "WorkflowGovernance", ct);
        if (setting == null) throw new CentralComplaintUnavailableException();
        using var document = JsonDocument.Parse(setting.Json);
        var reviewers = document.RootElement.GetProperty("IntakeReviewers").Clone();
        await tx.CommitAsync(ct);
        return (profiles, reviewers);
    }
    public async Task<bool> IsCommissionerAsync(Guid actor, CancellationToken ct)
    {
        var data = await LoadAsync(ct);
        return data.Profiles.TryGetValue(actor, out var p) && p.Role == "Officer" && p.Institution == "COMMISSIONER";
    }
    public async Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)
    {
        var data = await LoadAsync(ct);
        var result = new List<ComplaintOfficer>();
        foreach (var row in data.Reviewers.EnumerateArray())
        {
            if (!Guid.TryParse(row.GetProperty("ActorId").GetString(), out var id) || id == Guid.Empty ||
                !data.Profiles.TryGetValue(id, out var profile) || profile.Role != "Officer" || profile.Institution != "DS") continue;
            var office = row.GetProperty("OfficeCode").GetString()?.Trim();
            if (string.IsNullOrEmpty(office) || office.Length > 100 || office.Any(char.IsControl)) continue;
            var name = row.TryGetProperty("DisplayName", out var display) ? display.GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(name) || name.Length > 150 || name.Any(char.IsControl)) name = "DS officer · " + office;
            result.Add(new(id, name, office));
        }
        if (result.GroupBy(x => x.ActorId).Any(x => x.Count() != 1)) throw new CentralComplaintUnavailableException();
        var ordered = result.OrderBy(x => x.ActorId).ToArray();
        return new(ordered, ComplaintRoutingPolicy.Hash(JsonSerializer.Serialize(ordered)));
    }
}

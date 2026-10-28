using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using StateLandGovernance.LandIntelligence.Presentation.Security;
namespace StateLandGovernance.Api.Security;

public sealed class DevelopmentAccounts(IConfiguration configuration, IHostEnvironment environment,
    LandIntelligenceJwtTokenService tokens)
{
    private readonly ConcurrentDictionary<string, (string Username, DateTimeOffset Expiry)> sessions = new();
    public bool Enabled => configuration.GetValue<bool>("PortalAuthentication:Enabled") ||
        (environment.IsDevelopment() && configuration.GetValue<bool>("LocalDevelopmentSignIn:Enabled"));
    public bool Local(HttpContext context) => environment.IsDevelopment() && Enabled && context.Connection.RemoteIpAddress is { } ip &&
        System.Net.IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
    public bool CanSignIn(HttpContext context) => Enabled && (environment.IsDevelopment() ? Local(context) : context.Request.IsHttps);
    public IConfigurationSection? Account(string username) => configuration.GetSection("LocalDevelopmentSignIn:Accounts")
        .GetChildren().SingleOrDefault(a => string.Equals(a["Username"], username, StringComparison.OrdinalIgnoreCase));
    public object View(IConfigurationSection a) => new
    {
        username = a["Username"],
        displayName = a["DisplayName"],
        role = a["DisplayRole"],
        institution = a["Institution"],
        actorId = a["ActorId"],
        caseId = a["CaseId"],
        reference = a["CaseReference"],
        home = a["TokenRole"] == "Applicant" ? "/workflow-governance/applicant/cases" : a["Institution"] == "DS" ? "/workflow-governance/cases" : a["Home"],
        developmentOnly = environment.IsDevelopment()
    };
    public object? SignIn(string username, string password)
    {
        var a = Account(username); if (a is null || !Verify(password, a["PasswordSalt"], a["PasswordHash"])) return null;
        var actor = Guid.Parse(a["ActorId"]!); var role = a["TokenRole"]!;
        var source = new JwtSecurityTokenHandler().ReadJwtToken(tokens.CreateAccessToken(actor, new[] { role }));
        var expiry = DateTimeOffset.UtcNow.AddHours(8);
        var claims = source.Claims.Where(c => c.Type is not ("exp" or "nbf" or "iss" or "aud" or "jti")).ToList();
        claims.Add(new("development_account", a["Username"]!)); claims.Add(new("development_session", sid));
        var jwt = new JwtSecurityToken(source.Issuer, source.Audiences.Single(), claims, DateTime.UtcNow,
            expiry.UtcDateTime, new SigningCredentials(tokens.CreateValidationParameters().IssuerSigningKey, SecurityAlgorithms.HmacSha256));
        sessions[sid] = (a["Username"]!, expiry);
        return new { accessToken = new JwtSecurityTokenHandler().WriteToken(jwt), expiresAtUtc = expiry, user = View(a) };
    }
    public bool Validate(HttpContext http, ClaimsPrincipal principal)
    {
        var marker = principal.FindFirstValue("development_account");
        if (marker is null) return !Enabled; // Opt-in mode cannot be bypassed by the old arbitrary-profile issuer.
        if (!CanSignIn(http)) return false;
        var sid = principal.FindFirstValue("development_session"); var a = Account(marker);
        var actor = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        // Development signing configuration may be public. A valid live session still cannot carry
        // roles/scopes injected into a re-signed token: assignments are reapplied server-side only.
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (a is null || roles.Length != 1 || roles[0] != a["TokenRole"] ||
            principal.Claims.Any(c => c.Type == "capability" || c.Type == "institution" || c.Type.StartsWith("authority_", StringComparison.Ordinal))) return false;
        if (principal.FindFirstValue("land_intelligence_owner_id") != a["ActorId"]) return false;
        return a["ActorId"] == actor && sid is not null && sessions.TryGetValue(sid, out var s) &&
            s.Username == a["Username"] && s.Expiry > DateTimeOffset.UtcNow;
    }
    public void SignOut(ClaimsPrincipal principal) { if (principal.FindFirstValue("development_session") is { } sid) sessions.TryRemove(sid, out _); }
    public static bool Verify(string password, string? salt, string? hash)
    {
        if (password.Length > 256 || salt is null || hash is null) return false;
        try { return CryptographicOperations.FixedTimeEquals(Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), 100000, HashAlgorithmName.SHA256, 32), Convert.FromBase64String(hash)); }
        catch (FormatException) { return false; }
    }
}

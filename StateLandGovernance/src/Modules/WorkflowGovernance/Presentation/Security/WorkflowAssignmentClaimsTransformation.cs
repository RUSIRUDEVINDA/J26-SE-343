using System.Globalization;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Commands;

namespace StateLandGovernance.WorkflowGovernance.Presentation.Security;

/// <summary>Trusted deployment-owned assignments; never reads browser headers or request bodies.</summary>
public sealed class WorkflowAssignmentClaimsTransformation(IConfiguration configuration, IHttpContextAccessor? accessor = null, ICaseAssignmentStore? assignments = null) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
            return principal;
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var actorId)) return principal;
        // Token validity remains the upper bound; an assignment cannot extend it.
        if (!long.TryParse(principal.FindFirstValue("exp"), out var tokenExpiry) ||
            tokenExpiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return principal;
        // Existing host issuer names the officer role land_intelligence_officer. C3/C4 use Officer.
        // This compatibility alias carries the existing officer role only, never scoped capabilities.
        if (principal.IsInRole("land_intelligence_officer") && !principal.IsInRole("Officer"))
            identity.AddClaim(new Claim(identity.RoleClaimType, "Officer"));
        var routeCaseText = accessor?.HttpContext?.Request.RouteValues["caseId"]?.ToString();
        var hasRouteCase = Guid.TryParse(routeCaseText, out var routeCase);
        if (hasRouteCase && assignments != null && await assignments.ReadAsync(routeCase) is { } managed)
        {
            // Managed cases never fall back to legacy configuration after reassignment/revocation.
            // JWTs contain identity only. Re-read the active exact-case/document grant every request.
            foreach (var claim in identity.Claims.Where(c => c.Type.StartsWith("authority_") || c.Type is "capability" or "institution").ToArray())
                identity.RemoveClaim(claim);
            Add("authority_managed_case", routeCase.ToString("D"));
            if (managed.Active is { } active && active.ActorId == actorId && CaseAssignmentService.IsActive(active, DateTimeOffset.UtcNow))
            {
                Add("authority_case_id", routeCase.ToString("D")); Add("authority_document_id", active.DocumentId.ToString("D"));
                Add("institution", active.OfficeCode);
                foreach (var capability in active.Capabilities.Intersect(CaseAssignmentService.ProposalReviewCapabilities)) Add("capability", capability);
                foreach (var claim in identity.FindAll("exp").ToArray()) identity.RemoveClaim(claim);
                Add("exp", Math.Min(tokenExpiry, active.ValidUntil.ToUnixTimeSeconds()).ToString(CultureInfo.InvariantCulture));
                Add("authority_assignment_source", "Persisted intake assignment " + active.Id.ToString("D"));
            }
            else
            {
                // Responsibility revocation does not revoke a separately granted institutional request role.
                // Such a legacy grant must never include coordinating-workspace permissions.
                var institutionGrant = configuration.GetSection("WorkflowGovernance:AuthorityAssignments").GetChildren()
                    .FirstOrDefault(x => x["ActorId"] == actorId.ToString("D") && x["CaseId"] == routeCase.ToString("D") &&
                        !x.GetSection("Capabilities").GetChildren().Any(c => c.Value == "CaseWorkspaceReviewer") &&
                        !string.IsNullOrWhiteSpace(x["InstitutionCode"]) && !string.IsNullOrWhiteSpace(x["SourceReference"]) &&
                        DateTimeOffset.TryParse(x["ExpiresAtUtc"], out var expiry) && expiry > DateTimeOffset.UtcNow);
                if (institutionGrant != null)
                {
                    Add("authority_case_id", routeCase.ToString("D")); Add("institution", institutionGrant["InstitutionCode"]!);
                    foreach (var cap in institutionGrant.GetSection("Capabilities").GetChildren())
                        if (!string.IsNullOrWhiteSpace(cap.Value)) Add("capability", cap.Value!);
                    var expiry = DateTimeOffset.Parse(institutionGrant["ExpiresAtUtc"]!, CultureInfo.InvariantCulture);
                    foreach (var claim in identity.FindAll("exp").ToArray()) identity.RemoveClaim(claim);
                    Add("exp", Math.Min(tokenExpiry, expiry.ToUnixTimeSeconds()).ToString(CultureInfo.InvariantCulture));
                }
            }
            return principal;
        }
        foreach (var grant in configuration.GetSection("WorkflowGovernance:AuthorityAssignments").GetChildren())
        {
            if (hasRouteCase && grant["CaseId"] != routeCase.ToString("D")) continue;
            if (!Guid.TryParse(grant["ActorId"], out var assignedActor) || assignedActor != actorId ||
                !Guid.TryParse(grant["CaseId"], out var caseId) || caseId == Guid.Empty ||
                string.IsNullOrWhiteSpace(grant["SourceReference"]) ||
                !DateTimeOffset.TryParse(grant["ExpiresAtUtc"], CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var expires) || expires <= DateTimeOffset.UtcNow)
                continue;
            // Existing consumers pair repeated capabilities with a single case scope. Never union grants
            // across cases/institutions/documents: that would cross-product unrelated authorities.
            if (principal.HasClaim(c => c.Type is "authority_case_id" or "capability")) break;
            Add("authority_case_id", caseId.ToString("D"));
            if (Guid.TryParse(grant["DocumentId"], out var documentId) && documentId != Guid.Empty)
                Add("authority_document_id", documentId.ToString("D"));
            if (!string.IsNullOrWhiteSpace(grant["InstitutionCode"])) Add("institution", grant["InstitutionCode"]!);
            foreach (var capability in grant.GetSection("Capabilities").GetChildren())
                if (!string.IsNullOrWhiteSpace(capability.Value)) Add("capability", capability.Value!);
            // Existing authority checks consume exp. Narrow it to this assignment's expiry.
            foreach (var existing in identity.FindAll("exp").ToArray()) identity.RemoveClaim(existing);
            Add("exp", Math.Min(tokenExpiry, expires.ToUnixTimeSeconds()).ToString(CultureInfo.InvariantCulture));
            Add("authority_assignment_source", grant["SourceReference"]!);
            break;
        }
        return principal;
        void Add(string type, string value) => identity.AddClaim(new Claim(type, value));
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Application.Security;
using StateLandGovernance.LandIntelligence.Presentation.Security;

namespace StateLandGovernance.UnitTests.LandIntelligence.Security;

public sealed class LandIntelligenceAuthenticationTests
{
    private const string TestSigningKeyBase64 =
        "c2VjcmV0LWRldi1sYW5kLWludGVsbGlnZW5jZS1qd3Qta2V5LTMyYg==";

    private static LandIntelligenceJwtTokenService CreateTokenService()
    {
        Environment.SetEnvironmentVariable("LAND_INTELLIGENCE_JWT_SIGNING_KEY", TestSigningKeyBase64);
        var options = Options.Create(
            new LandIntelligenceAuthenticationOptions
            {
                JwtSigningKeyBase64 = TestSigningKeyBase64,
                Issuer = "state-land-governance",
                Audience = "land-intelligence",
            });

        return new LandIntelligenceJwtTokenService(options);
    }

    [Fact]
    public void CreateAccessToken_includes_subject_and_role_claims()
    {
        var service = CreateTokenService();
        var ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var token = service.CreateAccessToken(
            ownerId,
            [LandIntelligenceAuth.Roles.Applicant]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(ownerId.ToString("D"), jwt.Subject);
        Assert.Contains(
            jwt.Claims,
            c => c.Type == ClaimTypes.Role && c.Value == LandIntelligenceAuth.Roles.Applicant);
    }

    [Fact]
    public void OwnerIdResolver_reads_authenticated_applicant_subject()
    {
        var ownerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, ownerId.ToString("D")),
                        new Claim(LandIntelligenceAuth.ClaimTypes.OwnerId, ownerId.ToString("D")),
                        new Claim(ClaimTypes.Role, LandIntelligenceAuth.Roles.Applicant),
                    ],
                    LandIntelligenceAuth.Schemes.JwtBearer)),
        };

        var resolved = LandIntelligenceOwnerIdResolver.ResolveOwnerId(context);
        Assert.Equal(ownerId, resolved);
    }

    [Fact]
    public void ApplicantNic_is_masked_for_non_draft_api_responses()
    {
        var applicant = new LeaseInterestApplicantDto(
            LeaseInterestApplicantKindDto.Individual,
            "Test User",
            "+94110000000",
            "test@example.com",
            "Address",
            null,
            null,
            "200320400824",
            null);

        var masked = LeaseInterestApplicantNic.MaskForApiResponse(applicant);

        Assert.NotEqual("200320400824", masked.NationalIdentityCardNumber);
        Assert.Equal(SriLankanNic.Mask("200320400824"), masked.NationalIdentityCardNumber);
    }

    [Fact]
    public async Task Authorization_policies_require_expected_roles()
    {
        Environment.SetEnvironmentVariable("LAND_INTELLIGENCE_JWT_SIGNING_KEY", TestSigningKeyBase64);

        var hostEnvironment = new TestHostEnvironment { EnvironmentName = Environments.Development };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(hostEnvironment);
        services.AddLandIntelligenceAuthentication(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            hostEnvironment);

        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();

        var officerPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, LandIntelligenceAuth.Roles.Officer)],
                LandIntelligenceAuth.Schemes.JwtBearer));

        var applicantPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, LandIntelligenceAuth.Roles.Applicant)],
                LandIntelligenceAuth.Schemes.JwtBearer));

        var officerResult = await authorization.AuthorizeAsync(
            officerPrincipal,
            null,
            LandIntelligenceAuth.Policies.Officer);

        var applicantOnOfficerPolicy = await authorization.AuthorizeAsync(
            applicantPrincipal,
            null,
            LandIntelligenceAuth.Policies.Officer);

        Assert.True(officerResult.Succeeded);
        Assert.False(applicantOnOfficerPolicy.Succeeded);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

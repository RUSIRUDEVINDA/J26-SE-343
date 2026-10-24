using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Security;

namespace StateLandGovernance.LandIntelligence.Presentation.Security;

public static class LandIntelligenceAuthenticationExtensions
{
    public static IServiceCollection AddLandIntelligenceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<LandIntelligenceAuthenticationOptions>()
            .Bind(configuration.GetSection(LandIntelligenceAuthenticationOptions.SectionName));

        services.AddSingleton<LandIntelligenceJwtTokenService>();

        var authBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>>(sp =>
        {
            var tokenService = sp.GetRequiredService<LandIntelligenceJwtTokenService>();
            return new ConfigureNamedOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options => options.TokenValidationParameters = tokenService.CreateValidationParameters());
        });

        if (environment.IsDevelopment())
        {
            authBuilder.AddScheme<AuthenticationSchemeOptions, LandIntelligenceDemoOwnerAuthenticationHandler>(
                LandIntelligenceAuth.Schemes.DemoOwner,
                _ => { });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                LandIntelligenceAuth.Policies.Applicant,
                policy =>
                {
                    policy.AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        LandIntelligenceAuth.Schemes.DemoOwner);
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(LandIntelligenceAuth.Roles.Applicant);
                });

            options.AddPolicy(
                LandIntelligenceAuth.Policies.Officer,
                policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(LandIntelligenceAuth.Roles.Officer);
                });

            options.AddPolicy(
                LandIntelligenceAuth.Policies.Authority,
                policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(LandIntelligenceAuth.Roles.Authority);
                });

            options.AddPolicy(
                LandIntelligenceAuth.Policies.InternalMaintainer,
                policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole(LandIntelligenceAuth.Roles.InternalMaintainer);
                });
        });

        return services;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class ComplaintSandboxWriteGateTests
{
    sealed class Host(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "isolated-sandbox-gate-test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    public async Task Sandbox_DefaultDisabledOrNonDevelopment_DoesNotReachClassifier(string environment, bool optIn)
    {
        var controller = new GovernanceIntelligenceController(null!, null!, null!, null!, null!, null!)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["GovernanceIntelligence:ComplaintSandbox:WritesEnabled"] = optIn.ToString() }).Build();
        var response = Assert.IsType<ObjectResult>(await controller.ClassifyComplaint(
            new ClassifyComplaintCommand("fictional", "Isolated gate test"), null, default, config, new Host(environment)));
        Assert.Equal(503, response.StatusCode);
        Assert.Equal("Complaint classifier sandbox writes are disabled", Assert.IsType<ProblemDetails>(response.Value).Title);
    }

    [Fact]
    public void Sandbox_StillRequiresServerOfficerAuthorization()
    {
        var attribute = typeof(GovernanceIntelligenceController).GetMethod(nameof(GovernanceIntelligenceController.ClassifyComplaint))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("Officer,ComplianceOfficer,LandOfficer", attribute.Roles);
    }
}

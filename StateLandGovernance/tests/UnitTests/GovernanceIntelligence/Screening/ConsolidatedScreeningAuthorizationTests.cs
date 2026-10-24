namespace StateLandGovernance.UnitTests.GovernanceIntelligence.Screening;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Fixtures;

public sealed class ConsolidatedScreeningAuthorizationTests
{
    private static GovernanceIntelligenceController Controller(string caseId, string scenario)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")), new(ClaimTypes.Role, "Officer"),
            new("authority_case_id", scenario == "wrongCase" ? "OTHER" : caseId),
            new("exp", DateTimeOffset.UtcNow.AddHours(scenario == "expired" ? -1 : 1).ToUnixTimeSeconds().ToString()) };
        if (scenario != "institution") claims.Add(new("capability", "CaseWorkspaceReviewer"));
        if (scenario == "institution") claims.Add(new("institution", "EXTERNAL_TEST"));
        return new(null!, null!, null!, null!, null!, null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext {
            User = new(new ClaimsIdentity(claims, scenario == "anonymous" ? null : "EXPLICIT TEST ONLY")) } } };
    }
    [Theory]
    [InlineData("anonymous")][InlineData("expired")][InlineData("wrongCase")][InlineData("institution")]
    public async Task ConsolidatedResults_WithoutCurrentCaseWorkspaceAuthority_DoNotReadTheStore(string scenario)
    {
        var controller = Controller("TEST_CASE", scenario);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GetLatestScreeningForCase("TEST_CASE", null!, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GetScreeningHistoryForCase("TEST_CASE", 10, null!, default)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GetScreeningHistory("TEST_CASE", 10, null!, default)).StatusCode);
        if (scenario != "wrongCase")
            Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.GetScreeningAssessment(Guid.NewGuid(), null!, default)).StatusCode);
    }
    [Fact]
    public async Task ConsolidatedResults_AuthorizedOfficer_MayReadItsCaseOnly()
    {
        var controller = Controller("TEST_CASE", "authorized");
        Assert.IsType<NotFoundObjectResult>(await controller.GetLatestScreeningForCase("TEST_CASE", new InMemoryConsolidatedGovernanceScreeningStore(), default));
    }
    [Fact]
    public async Task DirectScreening_OfficerWorkspaceGrant_CannotBypassC3Prerequisites()
    {
        var input = C4DevelopmentCaseFixtureProvider.GetAgricultureProjectFixture();
        var controller = Controller(input.CaseId, "authorized");
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.OrchestrateScreening(input, null!, default)).StatusCode);
    }
}

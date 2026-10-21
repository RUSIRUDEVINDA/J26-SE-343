using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Presentation.Models.Responses;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = LandIntelligenceApiGroups.External)]
[Route("api/v1/land/research-demo")]
[Produces("application/json")]
public sealed class LandResearchDemoController : ControllerBase
{
    private readonly ResearchDemoOptions _options;
    private readonly ILeaseInterestOfficerAccessGate _officerAccessGate;

    public LandResearchDemoController(
        IOptions<ResearchDemoOptions> options,
        ILeaseInterestOfficerAccessGate officerAccessGate)
    {
        _options = options.Value;
        _officerAccessGate = officerAccessGate;
    }

    [HttpGet("config")]
    [ProducesResponseType(typeof(ResearchDemoConfigResponse), StatusCodes.Status200OK)]
    public ActionResult<ResearchDemoConfigResponse> GetConfig() =>
        Ok(
            new ResearchDemoConfigResponse(
                _options.Enabled,
                _officerAccessGate.IsNicLookupEnabled));
}

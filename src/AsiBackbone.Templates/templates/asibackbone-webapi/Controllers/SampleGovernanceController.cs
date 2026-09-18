using AsiBackbone.AspNetCore.Endpoints;
using Company.AsibackboneTemplate.Governance;
using Microsoft.AspNetCore.Mvc;

namespace Company.AsibackboneTemplate.Controllers;

[ApiController]
[Route("sample/controller")]
public sealed class SampleGovernanceController : ControllerBase
{
    [HttpPost("execute")]
    [GovernancePolicy(typeof(SampleEndpointPolicy))]
    [RequireCapabilityGrant("sample.execute")]
    [EmitGovernanceAudit]
    public IActionResult ExecuteGovernedAction()
    {
        return Ok(new
        {
            message = "Controller action executed after AsiBackbone endpoint governance metadata was evaluated."
        });
    }
}

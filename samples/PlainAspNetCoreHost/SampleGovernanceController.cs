using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace AsiBackbone.Samples.PlainAspNetCoreHost;

/// <summary>
/// Sample controller demonstrating the use of AsiBackbone governance attributes for endpoint governance, acknowledgment, and capability grant enforcement.
/// </summary>
[ApiController]
[Route("sample/ergonomic/controller-public")]
public sealed class SampleGovernanceController : ControllerBase
{
    /// <summary>
    /// Sample public controller action that requires endpoint governance evaluation, acknowledgment, and capability grant enforcement before execution.
    /// </summary>
    /// <returns>
    /// An IActionResult indicating the result of the action execution, including a message confirming successful execution after governance evaluation.
    /// </returns>
    [HttpPost]
    [GovernancePolicy(typeof(SampleControllerEndpointPolicy))]
    [RequireAcknowledgment]
    [RequireCapabilityGrant("sample.high-risk.execute")]
    [EmitGovernanceAudit]
    public IActionResult ExecuteHighRiskAction()
    {
        return Ok(new
        {
            message = "Public controller action executed after AsiBackbone endpoint governance metadata was evaluated."
        });
    }
}

/// <summary>
/// Sample governance policy class used for endpoint governance evaluation in the SampleGovernanceController.
/// </summary>
public sealed class SampleControllerEndpointPolicy
{
}

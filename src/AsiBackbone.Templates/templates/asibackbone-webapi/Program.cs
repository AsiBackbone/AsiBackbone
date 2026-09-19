using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using AsiBackbone.Storage.InMemory.Audit;
using Company.AsibackboneTemplate.Governance;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAsiBackboneAspNetCore();

builder.Services.AddSingleton<InMemoryAuditLedger>();
builder.Services.AddSingleton<IDecisionReceiptSink>(serviceProvider =>
    serviceProvider.GetRequiredService<InMemoryAuditLedger>());

builder.Services.AddSingleton<IEndpointCapabilityGrantValidator, SampleCapabilityGrantValidator>();
builder.Services.AddSingleton<IGovernanceConstraint<GovernanceEvaluationContext>, SampleRegionConstraint>();
builder.Services.AddSingleton<IGovernanceDecisionPolicy<GovernanceEvaluationContext>, SampleDecisionPolicy>();
builder.Services.AddSingleton<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>(serviceProvider =>
    DefaultGovernancePolicyEvaluator.CreateBuilder<GovernanceEvaluationContext>()
        .AddConstraints(serviceProvider.GetServices<IGovernanceConstraint<GovernanceEvaluationContext>>())
        .AddThreatModelContributors(serviceProvider.GetServices<IThreatModelContributor<GovernanceEvaluationContext>>())
        .WithDecisionPolicy(serviceProvider.GetService<IGovernanceDecisionPolicy<GovernanceEvaluationContext>>())
        .WithOptions(serviceProvider.GetRequiredService<IOptions<GovernancePolicyOptions>>().Value)
        .WithLogger(serviceProvider.GetService<ILogger<DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext>>>())
        .Build());

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
// Endpoint governance reads the selected endpoint, so it must run after routing. This host relies on
// WebApplication inserting UseRouting at the front of the pipeline; a host that calls UseRouting itself can
// pass requireEndpointRoutingRegistered: true to turn that ordering requirement into a startup failure.
app.UseAsiBackboneEndpointGovernance();

app.MapGet("/", () => Results.Redirect("/sample/decision"));

app.MapGet("/sample/decision", async (
    HttpContext httpContext,
    IGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator,
    IDecisionReceiptSink auditSink,
    CancellationToken cancellationToken) =>
{
    string correlationId = httpContext.TraceIdentifier;

    var context = new GovernanceEvaluationContext(
        correlationId: correlationId,
        policyVersion: "template-policy-v1",
        policyHash: "template-policy-hash",
        metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["operation"] = "template.sample.decision",
            ["region"] = "US-LA",
            ["risk"] = "routine",
            ["host_style"] = builder.Configuration["AsiBackbone:HostStyle"] ?? "plain"
        });

    GovernanceDecision decision = await evaluator
        .EvaluateAsync(context, cancellationToken)
        .ConfigureAwait(false);

    var residue = DecisionReceipt.FromDecision(
        GovernanceActorContext.Human("template-user", "Template User"),
        operationName: "template.sample.decision",
        decision,
        metadata: context.Metadata);

    await auditSink.WriteAsync(residue, cancellationToken).ConfigureAwait(false);

    return Results.Ok(new
    {
        decision = decision.Outcome.ToString(),
        decision.CanProceed,
        decision.RequiresAcknowledgment,
        decision.ReasonCodes,
        decision.CorrelationId,
        decision.PolicyVersion,
        decision.PolicyHash,
        auditEventId = residue.EventId,
        hostStyle = builder.Configuration["AsiBackbone:HostStyle"] ?? "plain",
        next = new[]
        {
            "POST /sample/minimal/execute",
            "POST /sample/controller/execute",
            "GET /sample/audit/{correlationId}"
        }
    });
})
.WithDisplayName("template.sample.decision")
.MarkGovernancePolicy(typeof(SampleEndpointPolicy))
.RequireCapabilityGrant("sample.execute")
.EmitGovernanceAudit();

app.MapPost("/sample/minimal/execute", () => Results.Ok(new
{
    message = "Minimal API endpoint executed after AsiBackbone endpoint governance metadata was evaluated."
}))
.WithDisplayName("template.sample.minimal.execute")
.MarkGovernancePolicy(typeof(SampleEndpointPolicy))
.RequireCapabilityGrant("sample.execute")
.EmitGovernanceAudit();

// Decision receipt records who attempted what and why it was allowed or denied, so reading it is a governed operation
// rather than an open lookup. It carries its own capability requirement, separate from executing the sample endpoints.
app.MapGet("/sample/audit/{correlationId}", (
    string correlationId,
    InMemoryAuditLedger auditLedger) => Results.Ok(auditLedger.GetByCorrelationId(correlationId)))
.WithDisplayName("template.sample.audit.read")
.MarkGovernancePolicy(typeof(SampleEndpointPolicy))
.RequireCapabilityGrant("sample.audit.read")
.EmitGovernanceAudit();

app.MapControllers();

app.Run();

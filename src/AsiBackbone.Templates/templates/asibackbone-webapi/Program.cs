using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Storage.InMemory.Audit;
using Company.AsibackboneTemplate.Governance;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAsiBackboneAspNetCore();

builder.Services.AddSingleton<InMemoryAuditLedger>();
builder.Services.AddSingleton<IAsiBackboneAuditSink>(serviceProvider =>
    serviceProvider.GetRequiredService<InMemoryAuditLedger>());

builder.Services.AddSingleton<IAsiBackboneEndpointCapabilityGrantValidator, SampleCapabilityGrantValidator>();
builder.Services.AddSingleton<IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>, SampleRegionConstraint>();
builder.Services.AddSingleton<IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>, SampleDecisionPolicy>();
builder.Services.AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>, DefaultAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAsiBackboneEndpointGovernance();

app.MapGet("/", () => Results.Redirect("/sample/decision"));

app.MapGet("/sample/decision", async (
    HttpContext httpContext,
    IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext> evaluator,
    IAsiBackboneAuditSink auditSink,
    CancellationToken cancellationToken) =>
{
    string correlationId = httpContext.TraceIdentifier;

    var context = new AsiBackboneConstraintEvaluationContext(
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

    var residue = AuditResidue.FromDecision(
        AsiBackboneActorContext.Human("template-user", "Template User"),
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

app.MapGet("/sample/audit/{correlationId}", (
    string correlationId,
    InMemoryAuditLedger auditLedger) => Results.Ok(auditLedger.GetByCorrelationId(correlationId)));

app.MapControllers();

app.Run();

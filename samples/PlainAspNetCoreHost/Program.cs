using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Signing;
using AsiBackbone.EntityFrameworkCore;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.Signing.LocalDevelopment;
using AsiBackbone.Storage.InMemory.Audit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAsiBackboneAspNetCore();

builder.Services.AddSingleton(LocalDevelopmentSigningOptions.Create(
    keyId: "sample-local-dev-key",
    keyVersion: "dev"));
builder.Services.AddSingleton<LocalDevelopmentSigningService>();
builder.Services.AddSingleton<IAsiBackboneSigningService>(serviceProvider =>
    serviceProvider.GetRequiredService<LocalDevelopmentSigningService>());
builder.Services.AddSingleton<IAsiBackboneSignatureVerificationService>(serviceProvider =>
    serviceProvider.GetRequiredService<LocalDevelopmentSigningService>());

builder.Services.AddDbContext<PlainHostAsiBackboneDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("AsiBackbone") ?? "Data Source=asi-backbone-sample.db"));

builder.Services.AddScoped<DbContext>(serviceProvider =>
    serviceProvider.GetRequiredService<PlainHostAsiBackboneDbContext>());
builder.Services.AddScoped<IAsiBackboneAuditLedgerStore, EfCoreAuditLedgerStore>();

builder.Services.AddSingleton<InMemoryAuditLedger>();
builder.Services.AddSingleton<IAsiBackboneAuditSink>(serviceProvider =>
    serviceProvider.GetRequiredService<InMemoryAuditLedger>());
builder.Services.AddSingleton<IAsiBackboneEndpointCapabilityGrantValidator, SampleEndpointCapabilityGrantValidator>();

builder.Services.AddSingleton<IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>, RegionConstraint>();
builder.Services.AddSingleton<IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>, ConsequentialActionDecisionPolicy>();
builder.Services.AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>, DefaultAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>();

WebApplication app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    PlainHostAsiBackboneDbContext dbContext = scope.ServiceProvider.GetRequiredService<PlainHostAsiBackboneDbContext>();
    _ = await dbContext.Database.EnsureCreatedAsync().ConfigureAwait(false);
}

app.UseAsiBackboneEndpointGovernance();

app.MapGet("/", () => Results.Redirect("/sample/decision"));

app.MapPost("/sample/ergonomic/minimal", () => Results.Ok(new
{
    message = "Minimal API endpoint executed after AsiBackbone endpoint governance metadata was evaluated."
}))
.WithDisplayName("sample.ergonomic.minimal")
.MarkGovernancePolicy(typeof(SampleEndpointPolicy))
.RequireLiabilityHandshake()
.RequireCapabilityGrant("sample.high-risk.execute")
.EmitGovernanceAudit();

app.MapGet("/sample/decision", async (
    HttpContext httpContext,
    IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext> evaluator,
    IAsiBackboneAuditSink auditSink,
    IAsiBackboneAuditLedgerStore ledgerStore,
    IAsiBackboneSigningService signingService,
    IAsiBackboneSignatureVerificationService verificationService,
    CancellationToken cancellationToken) =>
{
    string correlationId = httpContext.TraceIdentifier;

    var context = new AsiBackboneConstraintEvaluationContext(
        correlationId: correlationId,
        policyVersion: "sample-policy-v1",
        policyHash: "sample-policy-hash",
        metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["region"] = "US-LA",
            ["risk"] = "consequential",
            ["intent"] = "external-api-call"
        });

    GovernanceDecision decision = await evaluator
        .EvaluateAsync(context, cancellationToken)
        .ConfigureAwait(false);

    var actor = AsiBackboneActorContext.Human(
        actorId: "sample-user",
        displayName: "Sample User");

    var residue = AuditResidue.FromDecision(
        actor,
        "sample.external-api-call",
        decision,
        metadata: context.Metadata);

    await auditSink.WriteAsync(residue, cancellationToken).ConfigureAwait(false);

    var unsignedRecord = AuditLedgerRecord.FromResidue(residue);
    CanonicalPayload canonicalPayload = CanonicalPayloadBuilder.ForAuditLedgerRecord(unsignedRecord);
    CanonicalPayloadHash canonicalHash = CanonicalPayloadHasher.ComputeHash(canonicalPayload);
    var hashMetadata = canonicalHash.ToSigningMetadata();
    SigningResult signingResult = await signingService
        .SignAsync(
            new SigningRequest(
                canonicalHash.HashValue,
                canonicalHash.HashAlgorithm,
                purpose: CanonicalArtifactTypes.AuditLedgerRecord,
                keyId: "sample-local-dev-key",
                keyVersion: "dev",
                metadata: hashMetadata.Metadata),
            cancellationToken)
        .ConfigureAwait(false);
    SignatureVerificationResult verificationResult = await verificationService
        .VerifyAsync(
            new SignatureVerificationRequest(
                canonicalHash.HashValue,
                signingResult.Metadata,
                purpose: CanonicalArtifactTypes.AuditLedgerRecord),
            cancellationToken)
        .ConfigureAwait(false);

    var record = AuditLedgerRecord.FromResidue(
        residue,
        recordId: unsignedRecord.RecordId,
        recordedUtc: unsignedRecord.RecordedUtc,
        signingHash: signingResult.Metadata.SigningHash,
        signatureKeyId: signingResult.Metadata.KeyId,
        signatureKeyVersion: signingResult.Metadata.KeyVersion,
        signatureAlgorithm: signingResult.Metadata.SignatureAlgorithm,
        signatureValue: signingResult.Metadata.Signature,
        signatureProvider: signingResult.Metadata.Provider,
        signedUtc: signingResult.Metadata.SignedUtc,
        metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sample_signing_status"] = signingResult.IsSigned ? "signed" : "unsigned",
            ["sample_verification_status"] = verificationResult.Status
        });
    _ = await ledgerStore.AppendAsync(record, cancellationToken).ConfigureAwait(false);

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
        ledgerRecordId = record.RecordId,
        canonicalHash = canonicalHash.HashValue,
        signing = new
        {
            signingResult.IsSigned,
            signingResult.Metadata.KeyId,
            signingResult.Metadata.KeyVersion,
            signingResult.Metadata.SignatureAlgorithm,
            signingResult.Metadata.Provider,
            signingResult.Metadata.SignedUtc
        },
        verification = new
        {
            verificationResult.IsValid,
            verificationResult.Status,
            verificationResult.FailureCode
        }
    });
});

app.MapGet("/sample/audit/{correlationId}", (
    string correlationId,
    InMemoryAuditLedger auditLedger) => Results.Ok(auditLedger.GetByCorrelationId(correlationId)));

app.MapGet("/sample/ledger/{correlationId}", async (
    string correlationId,
    IAsiBackboneAuditLedgerStore ledgerStore,
    CancellationToken cancellationToken) =>
{
    IReadOnlyList<AuditLedgerRecord> records = await ledgerStore
        .FindByCorrelationIdAsync(correlationId, cancellationToken)
        .ConfigureAwait(false);

    return Results.Ok(records);
});

app.MapControllers();

// At startup, after building configuration:
IOptions<AsiBackboneEndpointGovernanceOptions> endpointOptions = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<AsiBackboneEndpointGovernanceOptions>>();
endpointOptions.Value.Validate(); // run once at startup and remove per-request Validate() calls

app.Run();

internal sealed class PlainHostAsiBackboneDbContext(DbContextOptions<PlainHostAsiBackboneDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        _ = modelBuilder.ApplyAsiBackboneConfigurations();
    }
}

internal sealed class RegionConstraint : IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>
{
    public string Name => "sample.region";

    public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        AsiBackboneConstraintEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool hasRegion = context.Metadata.TryGetValue("region", out string? region)
            && !string.IsNullOrWhiteSpace(region);

        bool hasEndpointPolicy = context.Metadata.ContainsKey("endpoint.policy_types");

        return ValueTask.FromResult(hasRegion || hasEndpointPolicy
            ? ConstraintEvaluationResult.Allow()
            : ConstraintEvaluationResult.Deny(
                "sample.region.missing",
                "A region is required before this host allows the operation to continue."));
    }
}

internal sealed class ConsequentialActionDecisionPolicy : IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>
{
    public ValueTask<GovernanceDecision> ApplyAsync(
        AsiBackboneConstraintEvaluationContext context,
        GovernanceDecision composedDecision,
        IReadOnlyList<ConstraintEvaluationResult> constraintResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!composedDecision.CanProceed)
        {
            return ValueTask.FromResult(composedDecision);
        }

        bool isConsequential = context.Metadata.TryGetValue("risk", out string? risk)
            && string.Equals(risk, "consequential", StringComparison.OrdinalIgnoreCase);

        return ValueTask.FromResult(isConsequential
            ? GovernanceDecision.RequireAcknowledgment(
                "sample.acknowledgment.required",
                "Consequential actions require host-owned acknowledgment before execution.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)
            : composedDecision);
    }
}

internal sealed class SampleEndpointPolicy
{
}

internal sealed class SampleEndpointCapabilityGrantValidator : IAsiBackboneEndpointCapabilityGrantValidator
{
    public ValueTask<GovernanceDecision> ValidateAsync(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        GovernanceDecision currentDecision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(currentDecision);

        return ValueTask.FromResult(descriptor.CapabilityScopes.Contains("sample.high-risk.execute", StringComparer.Ordinal)
            ? currentDecision
            : GovernanceDecision.Deny(
                "sample.capability.missing",
                "The sample endpoint capability grant validator did not find the required scope.",
                correlationId: currentDecision.CorrelationId,
                traceId: currentDecision.TraceId,
                policyVersion: currentDecision.PolicyVersion,
                policyHash: currentDecision.PolicyHash));
    }
}

[ApiController]
[Route("sample/ergonomic/controller")]
internal sealed class InternalSampleGovernanceController : ControllerBase
{
    [HttpPost]
    [RequireGovernancePolicy(typeof(SampleEndpointPolicy))]
    [RequireLiabilityHandshake]
    [RequireCapabilityGrant("sample.high-risk.execute")]
    [EmitGovernanceAudit]
    public IActionResult ExecuteHighRiskAction()
    {
        return Ok(new
        {
            message = "Controller action executed after AsiBackbone endpoint governance metadata was evaluated."
        });
    }
}

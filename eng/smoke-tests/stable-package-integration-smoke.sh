#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
configuration="${CONFIGURATION:-Release}"
package_output="${STABLE_SMOKE_PACKAGE_OUTPUT:-artifacts/stable-smoke-packages}"
work_root="${STABLE_SMOKE_WORK_ROOT:-${RUNNER_TEMP:-/tmp}/asi-backbone-stable-package-integration-smoke}"

get_central_package_version() {
  local package_id="$1"

  sed -nE "s/.*PackageVersion Include=\"$package_id\" Version=\"([^\"]+)\".*/\1/p" \
    "$repo_root/Directory.Packages.props" | head -n 1
}

make_absolute_path() {
  local path="$1"

  if [[ "$path" = /* || "$path" =~ ^[A-Za-z]:[\\/].* ]]; then
    printf '%s\n' "$path"
  else
    printf '%s/%s\n' "$repo_root" "$path"
  fi
}

to_dotnet_path() {
  local path="$1"

  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$path"
  else
    printf '%s\n' "$path"
  fi
}

package_output="$(to_dotnet_path "$(make_absolute_path "$package_output")")"

core_project="$repo_root/src/AsiBackbone.Core/AsiBackbone.Core.csproj"
package_version="${SMOKE_PACKAGE_VERSION:-$(dotnet msbuild "$core_project" -getProperty:Version -nologo | tr -d '\r' | awk 'NF { print; exit }')}"

if [ -z "$package_version" ]; then
  echo "Unable to determine package version. Set SMOKE_PACKAGE_VERSION explicitly."
  exit 1
fi

rm -rf "$package_output" "$work_root"
mkdir -p "$package_output" "$work_root"

mapfile -t package_projects < <(find "$repo_root/src" -name '*.csproj' -type f -not -path '*/templates/*' | sort)

if [ "${#package_projects[@]}" -eq 0 ]; then
  echo "No package projects were found under ./src."
  exit 1
fi

for project in "${package_projects[@]}"; do
  echo "Packing $project"
  dotnet pack "$project" \
    --configuration "$configuration" \
    --output "$package_output" \
    -p:ContinuousIntegrationBuild=true
  echo
done

# Use a fresh consumer cache: release-branch builds can share a version with published 5.x packages.
export NUGET_PACKAGES="$(to_dotnet_path "$work_root/.nuget/packages")"

cat > "$work_root/NuGet.config" <<NUGETCONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-asi-backbone" value="$package_output" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-asi-backbone">
      <package pattern="AsiBackbone.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
NUGETCONFIG

smoke_project_dir="$work_root/StablePackageIntegrationSmoke.Tests"
smoke_project="$smoke_project_dir/StablePackageIntegrationSmoke.Tests.csproj"

test_sdk_version="$(get_central_package_version "Microsoft.NET.Test.Sdk")"
xunit_version="$(get_central_package_version "xunit.v3")"
xunit_runner_version="$(get_central_package_version "xunit.runner.visualstudio")"

if [ -z "$test_sdk_version" ] || [ -z "$xunit_version" ] || [ -z "$xunit_runner_version" ]; then
  echo "Unable to resolve MTP/xUnit test package versions from Directory.Packages.props."
  exit 1
fi

mkdir -p "$smoke_project_dir"

cat > "$smoke_project" <<CSPROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$test_sdk_version" />
    <PackageReference Include="xunit.runner.visualstudio" Version="$xunit_runner_version">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="xunit.v3" Version="$xunit_version" />
  </ItemGroup>
</Project>
CSPROJ

pushd "$work_root" > /dev/null

ef_sqlite_version="$(get_central_package_version "Microsoft.EntityFrameworkCore.Sqlite")"
sqlitepclraw_version="$(get_central_package_version "SQLitePCLRaw.bundle_e_sqlite3")"

if [ -z "$ef_sqlite_version" ] || [ -z "$sqlitepclraw_version" ]; then
  echo "Unable to resolve smoke-test SQLite package versions from Directory.Packages.props."
  exit 1
fi

dotnet add "$smoke_project" package AsiBackbone.Core --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.AspNetCore --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.EntityFrameworkCore --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.Storage.InMemory --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.Analyzers --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.OpenTelemetry --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.Signing.LocalDevelopment --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.Signing.ManagedKey --version "$package_version"
dotnet add "$smoke_project" package Microsoft.AspNetCore.TestHost
dotnet add "$smoke_project" package Microsoft.EntityFrameworkCore.Sqlite --version "$ef_sqlite_version"
dotnet add "$smoke_project" package SQLitePCLRaw.bundle_e_sqlite3 --version "$sqlitepclraw_version"

popd > /dev/null

cat > "$smoke_project_dir/StablePackageIntegrationSmokeTests.cs" <<'CSHARP'
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using AsiBackbone.AspNetCore.Actors;
using AsiBackbone.AspNetCore.CapabilityGrants;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Acknowledgments;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Results;
using AsiBackbone.Core.Signing;
using AsiBackbone.EntityFrameworkCore;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.EntityFrameworkCore.CapabilityGrants;
using AsiBackbone.Signing.ManagedKey;
using AsiBackbone.Storage.InMemory.Audit;
using AsiBackbone.Storage.InMemory.CapabilityGrants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace StablePackageIntegrationSmoke.Tests;

public sealed class StablePackageIntegrationSmokeTests
{
    private static readonly DateTimeOffset CapabilityGrantNow = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CoreAndInMemoryAuditPackagesComposeDecisionAndStoreReceipt()
    {
        var evaluator = new DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext>(
            [new StableRegionConstraint()], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        string correlationId = $"stable-core-{Guid.NewGuid():N}";
        var context = new GovernanceEvaluationContext(
            correlationId: correlationId,
            policyVersion: "stable-package-policy-v1",
            policyHash: "stable-package-policy-hash",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = "US-LA",
                ["risk"] = "routine"
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(GovernanceDecisionOutcome.Allowed, decision.Outcome);
        Assert.True(decision.CanProceed);
        Assert.Equal(correlationId, decision.CorrelationId);

        IGovernanceActorContext actor = GovernanceActorContext.Human("stable-user", "Stable User");
        DecisionReceipt receipt = DecisionReceipt.FromDecision(
            actor,
            "stable.core.allow",
            decision,
            metadata: context.Metadata);

        var ledger = new InMemoryAuditLedger();

        await ledger.WriteAsync(
            receipt,
            TestContext.Current.CancellationToken);

        IDecisionReceipt stored = Assert.Single(ledger.Records);
        Assert.Equal(receipt.EventId, stored.EventId);
        Assert.Equal(correlationId, stored.CorrelationId);
        Assert.Single(ledger.GetByCorrelationId(correlationId));
        Assert.Same(stored, ledger.GetByEventId(receipt.EventId));
    }

    [Fact]
    public async Task PackagedInMemoryCapabilityGrantUseStoreAcceptsFirstUseAndDeniesReplay()
    {
        SignedGovernanceArtifact<CapabilityGrant> signedGrant = CreateSignedCapabilityGrant();
        var useStore = new InMemoryCapabilityGrantUseStore();
        CapabilityGrantValidationOptions options = CapabilityGrantValidationOptions.Create(
            issuer: "stable-issuer",
            audience: "stable-gateway",
            scopes: ["stable.execute"],
            validationUtc: CapabilityGrantNow,
            policyVersion: "stable-capability-policy-v1",
            policyHash: "stable-capability-policy-hash",
            requireUseCheck: true,
            maxUseCount: 1);

        CapabilityGrantValidationResult first = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            options,
            useStore: useStore,
            cancellationToken: TestContext.Current.CancellationToken);
        CapabilityGrantValidationResult second = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            options,
            useStore: useStore,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(first.IsValid);
        Assert.True(first.ShouldAllow);
        Assert.False(second.IsValid);
        Assert.Equal(CapabilityGrantValidationCategory.ReuseLimitExceeded, second.Category);
        Assert.Equal(VerificationPolicyAction.Deny, second.Action);
        Assert.Equal("capability.use-limit-exceeded", second.FailureCode);
        Assert.Equal(1, useStore.GetUseCount(signedGrant.Artifact.TokenId));
    }

    [Fact]
    public async Task ManagedKeySignedGrantVerifiesAndEfCoreStoreDeniesReplay()
    {
        using RSA rsa = RSA.Create(2048);
        var signingService = new ManagedKeySigningService(
            ManagedKeySigningOptions.Create("stable-managed-key", "v1"),
            new StableManagedKeyClient(rsa));
        ServiceProvider verificationProvider = new ServiceCollection()
            .AddAsiBackboneKeySignatureVerification(options => options.AddPemKey(
                "stable-managed-key",
                "v1",
                ManagedKeySignatureAlgorithms.RsaPssSha256,
                rsa.ExportSubjectPublicKeyInfoPem()))
            .BuildServiceProvider();
        IGovernanceSignatureVerificationService verifier = verificationProvider
            .GetRequiredService<IGovernanceSignatureVerificationService>();
        CapabilityGrant grant = CapabilityGrant.Create(
            tokenId: $"stable-managed-grant-{Guid.NewGuid():N}",
            issuer: "stable-managed-issuer",
            audience: "stable-managed-audience",
            scopes: ["stable.execute"],
            issuedUtc: CapabilityGrantNow.AddMinutes(-1),
            expiresUtc: CapabilityGrantNow.AddMinutes(5));
        SignedGovernanceArtifact<CapabilityGrant> signed = await GovernanceArtifactSigner.SignCapabilityGrantAsync(
            grant, signingService, cancellationToken: TestContext.Current.CancellationToken);

        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        DbContextOptions<StableSmokeDbContext> dbOptions = new DbContextOptionsBuilder<StableSmokeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new StableSmokeDbContext(dbOptions);
        _ = await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var useStore = new EfCoreCapabilityGrantUseStore(
            dbContext,
            new EfCoreCapabilityGrantUseStoreOptions { RetentionGracePeriod = TimeSpan.FromSeconds(30) });
        CapabilityGrantValidationOptions validation = CapabilityGrantValidationOptions.Create(
            issuer: "stable-managed-issuer",
            audience: "stable-managed-audience",
            scopes: ["stable.execute"],
            validationUtc: CapabilityGrantNow,
            requireProof: true,
            requireUseCheck: true,
            maxUseCount: 1);

        CapabilityGrantValidationResult first = await CapabilityGrantValidator.ValidateAsync(
            signed, validation, verifier, useStore, TestContext.Current.CancellationToken);
        CapabilityGrantValidationResult replay = await CapabilityGrantValidator.ValidateAsync(
            signed, validation, verifier, useStore, TestContext.Current.CancellationToken);

        Assert.True(first.IsValid);
        Assert.Equal(CapabilityGrantValidationCategory.ReuseLimitExceeded, replay.Category);
    }

    [Fact]
    public async Task SignedCapabilityGrantRoundTripsThroughPackagedEndpointValidator()
    {
        using RSA rsa = RSA.Create(2048);
        var issuerSigning = new ManagedKeySigningService(
            ManagedKeySigningOptions.Create("stable-configured-key", "v1"),
            new StableManagedKeyClient(rsa));
        await using var replayConnection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await replayConnection.OpenAsync(TestContext.Current.CancellationToken);

        Dictionary<string, StringValues> fields = new(StringComparer.Ordinal)
        {
            ["orderId"] = "stable-order-1",
            ["amount"] = "42.00"
        };
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CapabilityGrant grant = CapabilityGrant.Create(
            tokenId: $"stable-signed-grant-{Guid.NewGuid():N}",
            issuer: "stable-issuer",
            audience: "stable-orders",
            scopes: ["stable.orders.approve"],
            issuedUtc: now.AddMinutes(-1),
            expiresUtc: now.AddMinutes(5),
            subjectId: "stable-user",
            operationName: "stable.orders.approve",
            resourceBinding: FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(fields),
            maxUseCount: 1);

        SignedGovernanceArtifact<CapabilityGrant> signed = await GovernanceArtifactSigner.SignCapabilityGrantAsync(
            grant,
            issuerSigning,
            cancellationToken: TestContext.Current.CancellationToken);
        string token = CapabilityGrantToken.Encode(signed);

        var sink = new CapturingReceiptSink();
        ServiceCollection services = new();
        services.AddAsiBackboneAspNetCore();
        services.AddAsiBackboneSignedCapabilityGrants(options =>
        {
            options.Issuer = "stable-issuer";
            options.Audience = "stable-orders";
            options.AllowedClockSkew = TimeSpan.FromSeconds(30);
        });
        services.AddAsiBackboneFormFieldCapabilityGrantBinding();
        services.AddAsiBackboneKeySignatureVerification(options => options.AddPemKey(
            "stable-configured-key",
            "v1",
            ManagedKeySignatureAlgorithms.RsaPssSha256,
            rsa.ExportSubjectPublicKeyInfoPem()));
        services.AddDbContext<StableSmokeDbContext>(options => options.UseSqlite(replayConnection));
        services.AddAsiBackboneEfCoreCapabilityGrantUseStore<StableSmokeDbContext>(options =>
            options.RetentionGracePeriod = TimeSpan.FromSeconds(30));
        services.AddSingleton<IDecisionReceiptSink>(sink);
        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            _ = await setupScope.ServiceProvider.GetRequiredService<StableSmokeDbContext>()
                .Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new RequireCapabilityGrantAttribute("stable.orders.approve"),
                new EmitGovernanceAuditAttribute()),
            "stable.orders.approve");

        EndpointGovernanceResult first = await EvaluateSignedGrantRequestAsync(provider, endpoint, token, fields);
        EndpointGovernanceResult replay = await EvaluateSignedGrantRequestAsync(provider, endpoint, token, fields);

        Assert.True(first.CanExecute);
        Assert.False(replay.CanExecute);
        Assert.Contains(SignedCapabilityGrantReasonCodes.UseLimitExceeded, replay.Decision!.ReasonCodes);

        IDecisionReceipt allowedReceipt = sink.Records[0];
        Assert.Equal(grant.TokenId, allowedReceipt.CapabilityGrantId);
        Assert.Equal(grant.TokenId, AuditLedgerRecord.FromDecisionReceipt(allowedReceipt).CapabilityTokenId);
        Assert.Null(sink.Records[1].CapabilityGrantId);
    }

    private static async Task<EndpointGovernanceResult> EvaluateSignedGrantRequestAsync(
        ServiceProvider provider,
        Endpoint endpoint,
        string token,
        Dictionary<string, StringValues> fields)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(fields, StringComparer.OrdinalIgnoreCase));
        httpContext.Request.Headers[SignedCapabilityGrantOptions.DefaultTokenHeaderName] = token;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "stable-user")],
            authenticationType: "stable-smoke"));
        httpContext.SetEndpoint(endpoint);
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        return await scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>()
            .EvaluateAsync(httpContext, EndpointGovernanceDescriptor.FromEndpoint(endpoint), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StubbedReceiptSinkCapturesReceiptUsingPublicContract()
    {
        var sink = new CapturingReceiptSink();
        IDecisionReceiptSink receiptSink = sink;
        string correlationId = $"stable-stub-{Guid.NewGuid():N}";

        GovernanceDecision decision = GovernanceDecision.RequireAcknowledgment(
            "stable.acknowledgment.required",
            "Stable smoke test requires acknowledgment.",
            correlationId: correlationId,
            policyVersion: "stable-package-policy-v1",
            policyHash: "stable-package-policy-hash");

        DecisionReceipt receipt = DecisionReceipt.FromDecision(
            GovernanceActorContext.Service("stable-service", "Stable Service"),
            "stable.stubbed-sink.acknowledgment",
            decision,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sink"] = "stub",
                ["release"] = "stable-package-smoke"
            });

        await receiptSink.WriteAsync(
            receipt,
            TestContext.Current.CancellationToken);

        IDecisionReceipt captured = Assert.Single(sink.Records);
        Assert.Equal(receipt.EventId, captured.EventId);
        Assert.Equal(correlationId, captured.CorrelationId);
        Assert.Equal(nameof(GovernanceDecisionOutcome.AcknowledgmentRequired), captured.Outcome);
        Assert.Contains("stable.acknowledgment.required", captured.ReasonCodes);
        Assert.Equal("stub", captured.Metadata["sink"]);
    }

    [Fact]
    public async Task AspNetCoreAndEfCoreSqlitePackagesComposeHostOwnedWorkflow()
    {
        await using WebApplication app = await StableSmokeHost.BuildAsync();

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();

        StableHostResponse response = await GetAsync<StableHostResponse>(client, "/stable-smoke");

        Assert.True(response.AspNetCoreServicesResolved);
        Assert.Equal(nameof(GovernanceDecisionOutcome.Allowed), response.Decision);
        Assert.True(response.EfLedgerRecordFound);
        Assert.False(string.IsNullOrWhiteSpace(response.LedgerRecordId));
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string requestUri)
    {
        HttpResponseMessage response = await client.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();

        T? value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);

        return value;
    }

    private static SignedGovernanceArtifact<CapabilityGrant> CreateSignedCapabilityGrant()
    {
        CapabilityGrant grant = CapabilityGrant.Create(
            tokenId: "stable-capability-grant",
            issuer: "stable-issuer",
            audience: "stable-gateway",
            scopes: ["stable.execute"],
            issuedUtc: CapabilityGrantNow.AddMinutes(-5),
            expiresUtc: CapabilityGrantNow.AddMinutes(5),
            policyVersion: "stable-capability-policy-v1",
            policyHash: "stable-capability-policy-hash");

        // Built through the shared builder so the smoke test signs every grant field, matching what a
        // consumer of the stable package should do.
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityGrant(grant);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);
        var signingMetadata = SigningMetadata.Create(
            signingHash: hash.HashValue,
            hashAlgorithm: hash.HashAlgorithm,
            signature: "fake-signature",
            signatureAlgorithm: "FAKE-SIGNATURE-V1",
            keyId: "key-1",
            keyVersion: "v1",
            provider: "fake-provider",
            signedUtc: CapabilityGrantNow);

        return SignedGovernanceArtifacts.FromSigningMetadata(grant, payload, hash, signingMetadata);
    }
}

internal sealed class StableManagedKeyClient(RSA rsa) : IManagedKeySigningClient
{
    public ValueTask<ManagedKeySignResult> SignAsync(
        ManagedKeySignRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string signature = Convert.ToBase64String(rsa.SignData(
            request.SignatureInput.Span,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss));
        return ValueTask.FromResult(ManagedKeySignResult.Create(
            signature,
            request.SignatureAlgorithm,
            request.KeyId,
            request.KeyVersion,
            DateTimeOffset.UtcNow));
    }
}

internal static class StableSmokeHost
{
    public static async Task<WebApplication> BuildAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(StableSmokeHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.WebHost.UseTestServer();

        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"asi-backbone-stable-smoke-{Guid.NewGuid():N}.db");

        builder.Services.AddAsiBackboneAspNetCore();
        builder.Services.AddDbContext<StableSmokeDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        builder.Services.AddScoped<DbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<StableSmokeDbContext>());
        builder.Services.AddScoped<IGovernanceAuditLedgerStore, EfCoreAuditLedgerStore>();

        WebApplication app = builder.Build();

        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            StableSmokeDbContext dbContext = scope.ServiceProvider.GetRequiredService<StableSmokeDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        app.MapGet("/stable-smoke", async (
            IHttpGovernanceActorContextResolver actorResolver,
            IHttpGovernanceRequestCorrelationResolver correlationResolver,
            IAcknowledgmentChallengeService challengeService,
            IGovernanceAuditLedgerStore ledgerStore,
            CancellationToken cancellationToken) =>
        {
            string correlationId = $"stable-http-{Guid.NewGuid():N}";
            GovernanceDecision decision = GovernanceDecision.Allow(
                correlationId: correlationId,
                traceId: "stable-trace",
                policyVersion: "stable-http-policy-v1",
                policyHash: "stable-http-policy-hash");

            DecisionReceipt receipt = DecisionReceipt.FromDecision(
                GovernanceActorContext.Service("stable-http-host", "Stable HTTP Host"),
                "stable.http.allow",
                decision,
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["host"] = "aspnetcore",
                    ["storage"] = "sqlite"
                });

            AuditLedgerRecord record = AuditLedgerRecord.FromDecisionReceipt(receipt);
            OperationResult<AuditLedgerRecord> appendResult = await ledgerStore
                .AppendAsync(record, cancellationToken)
                .ConfigureAwait(false);

            if (appendResult.Failed)
            {
                return Results.Problem("Stable package smoke-test ledger append failed.");
            }

            AuditLedgerRecord? found = await ledgerStore
                .FindByRecordIdAsync(appendResult.Value.RecordId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new StableHostResponse(
                AspNetCoreServicesResolved: actorResolver is not null && correlationResolver is not null && challengeService is not null,
                Decision: decision.Outcome.ToString(),
                CorrelationId: correlationId,
                LedgerRecordId: appendResult.Value.RecordId,
                EfLedgerRecordFound: found is not null));
        });

        return app;
    }
}

internal sealed class StableSmokeDbContext(DbContextOptions<StableSmokeDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        _ = modelBuilder.ApplyAsiBackboneConfigurations();
    }
}

internal sealed class StableRegionConstraint : IGovernanceConstraint<GovernanceEvaluationContext>
{
    public string Name => "stable.region";

    public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        GovernanceEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool hasRegion = context.Metadata.TryGetValue("region", out string? region)
            && !string.IsNullOrWhiteSpace(region);

        return ValueTask.FromResult(hasRegion
            ? ConstraintEvaluationResult.Allow()
            : ConstraintEvaluationResult.Deny(
                "stable.region.missing",
                "Stable package smoke test requires region metadata."));
    }
}

internal sealed class CapturingReceiptSink : IDecisionReceiptSink
{
    private readonly List<IDecisionReceipt> records = [];

    public IReadOnlyList<IDecisionReceipt> Records => records.AsReadOnly();

    public ValueTask WriteAsync(
        IDecisionReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        cancellationToken.ThrowIfCancellationRequested();

        records.Add(receipt);

        return ValueTask.CompletedTask;
    }
}

internal sealed record StableHostResponse(
    bool AspNetCoreServicesResolved,
    string Decision,
    string CorrelationId,
    string LedgerRecordId,
    bool EfLedgerRecordFound);
CSHARP

dotnet test --project "$smoke_project" --configuration "$configuration" --verbosity normal

echo "Stable package integration smoke tests completed successfully."

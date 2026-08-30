#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
configuration="${CONFIGURATION:-Release}"
package_output="${PACKAGE_OUTPUT:-artifacts/packages}"
work_root="${SMOKE_WORK_ROOT:-${RUNNER_TEMP:-/tmp}/asi-backbone-external-consumer-smoke}"

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

cat > "$work_root/NuGet.config" <<NUGETCONFIG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-asi-backbone" value="$package_output" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
NUGETCONFIG

smoke_project_dir="$work_root/ExternalConsumerSmoke.Tests"
smoke_project="$smoke_project_dir/ExternalConsumerSmoke.Tests.csproj"

dotnet new xunit \
  --name ExternalConsumerSmoke.Tests \
  --output "$smoke_project_dir" \
  --framework net10.0 \
  --no-restore

pushd "$work_root" > /dev/null

ef_sqlite_version="$(get_central_package_version "Microsoft.EntityFrameworkCore.Sqlite")"
sqlitepclraw_version="$(get_central_package_version "SQLitePCLRaw.bundle_e_sqlite3")"

if [ -z "$ef_sqlite_version" ] || [ -z "$sqlitepclraw_version" ]; then
  echo "Unable to resolve smoke-test SQLite package versions from Directory.Packages.props."
  exit 1
fi

dotnet add "$smoke_project" package AsiBackbone.AspNetCore --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.EntityFrameworkCore --version "$package_version"
dotnet add "$smoke_project" package AsiBackbone.Storage.InMemory --version "$package_version"
dotnet add "$smoke_project" package Microsoft.AspNetCore.TestHost
dotnet add "$smoke_project" package Microsoft.EntityFrameworkCore.Sqlite --version "$ef_sqlite_version"
dotnet add "$smoke_project" package SQLitePCLRaw.bundle_e_sqlite3 --version "$sqlitepclraw_version"

popd > /dev/null

cat > "$smoke_project_dir/ExternalConsumerSmokeTests.cs" <<'CSHARP'
using System.Net.Http.Json;
using AsiBackbone.AspNetCore.Actors;
using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Handshakes;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Results;
using AsiBackbone.EntityFrameworkCore;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.Storage.InMemory.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExternalConsumerSmoke.Tests;

public sealed class ExternalConsumerSmokeTests
{
    [Fact]
    public async Task FreshHostWiresPackagesAndExercisesHttpDecisionFlows()
    {
        await using WebApplication app = await SmokeHost.BuildAsync();
        await app.StartAsync();

        using HttpClient client = app.GetTestClient();

        SmokeRegistrationResponse registrations = await GetAsync<SmokeRegistrationResponse>(client, "/diagnostics/registrations");
        Assert.True(registrations.AspNetCoreAdapterResolved);
        Assert.True(registrations.HostOwnedDbContextResolved);
        Assert.True(registrations.EfLedgerStoreResolved);
        Assert.True(registrations.InMemoryLedgerResolved);

        SmokeDecisionResponse allowed = await GetAsync<SmokeDecisionResponse>(client, "/decisions/allow");
        Assert.Equal(nameof(GovernanceDecisionOutcome.Allowed), allowed.Decision);
        Assert.True(allowed.CanProceed);
        Assert.False(allowed.RequiresAcknowledgment);
        Assert.Empty(allowed.ReasonCodes);
        Assert.Equal(1, allowed.EfLedgerRecordCount);

        SmokeDecisionResponse denied = await GetAsync<SmokeDecisionResponse>(client, "/decisions/deny");
        Assert.Equal(nameof(GovernanceDecisionOutcome.Denied), denied.Decision);
        Assert.False(denied.CanProceed);
        Assert.False(denied.RequiresAcknowledgment);
        Assert.Contains("smoke.region.missing", denied.ReasonCodes);
        Assert.Equal(1, denied.EfLedgerRecordCount);

        SmokeDecisionResponse acknowledgmentRequired = await GetAsync<SmokeDecisionResponse>(client, "/decisions/ack");
        Assert.Equal(nameof(GovernanceDecisionOutcome.AcknowledgmentRequired), acknowledgmentRequired.Decision);
        Assert.False(acknowledgmentRequired.CanProceed);
        Assert.True(acknowledgmentRequired.RequiresAcknowledgment);
        Assert.Contains("smoke.acknowledgment.required", acknowledgmentRequired.ReasonCodes);
        Assert.Equal(1, acknowledgmentRequired.EfLedgerRecordCount);

        InMemoryAuditLedger inMemoryLedger = app.Services.GetRequiredService<InMemoryAuditLedger>();
        Assert.Equal(3, inMemoryLedger.Records.Count);
        Assert.Single(inMemoryLedger.GetByCorrelationId(allowed.CorrelationId));
        Assert.Single(inMemoryLedger.GetByCorrelationId(denied.CorrelationId));
        Assert.Single(inMemoryLedger.GetByCorrelationId(acknowledgmentRequired.CorrelationId));
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string requestUri)
    {
        HttpResponseMessage response = await client.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();

        T? value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);

        return value;
    }
}

internal static class SmokeHost
{
    public static async Task<WebApplication> BuildAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SmokeHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.WebHost.UseTestServer();

        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"asi-backbone-external-consumer-{Guid.NewGuid():N}.db");

        builder.Services.AddAsiBackboneAspNetCore();
        builder.Services.AddDbContext<SmokeHostDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
        builder.Services.AddScoped<DbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<SmokeHostDbContext>());
        builder.Services.AddScoped<IAsiBackboneAuditLedgerStore, EfCoreAuditLedgerStore>();

        builder.Services.AddSingleton<InMemoryAuditLedger>();
        builder.Services.AddSingleton<IAsiBackboneAuditSink>(serviceProvider =>
            serviceProvider.GetRequiredService<InMemoryAuditLedger>());

        builder.Services.AddSingleton<IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>, SmokeRegionConstraint>();
        builder.Services.AddSingleton<IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>, SmokeDecisionPolicy>();
        builder.Services.AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>, DefaultAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>();

        WebApplication app = builder.Build();

        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            SmokeHostDbContext dbContext = scope.ServiceProvider.GetRequiredService<SmokeHostDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        app.MapGet("/diagnostics/registrations", (
            IAsiBackboneHttpActorContextResolver actorResolver,
            IAsiBackboneHttpRequestCorrelationResolver correlationResolver,
            IAsiBackboneAcknowledgmentChallengeService challengeService,
            SmokeHostDbContext dbContext,
            IAsiBackboneAuditLedgerStore ledgerStore,
            InMemoryAuditLedger inMemoryLedger) => Results.Ok(new SmokeRegistrationResponse(
                AspNetCoreAdapterResolved: actorResolver is not null && correlationResolver is not null && challengeService is not null,
                HostOwnedDbContextResolved: dbContext is not null,
                EfLedgerStoreResolved: ledgerStore is not null,
                InMemoryLedgerResolved: inMemoryLedger is not null)));

        app.MapGet("/decisions/{mode}", async (
            string mode,
            HttpContext httpContext,
            IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext> evaluator,
            IAsiBackboneAuditSink auditSink,
            IAsiBackboneAuditLedgerStore ledgerStore,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyDictionary<string, string> metadata = BuildMetadata(mode);
            string correlationId = $"smoke-{mode}-{Guid.NewGuid():N}";

            var context = new AsiBackboneConstraintEvaluationContext(
                correlationId: correlationId,
                policyVersion: "external-smoke-policy-v1",
                policyHash: "external-smoke-policy-hash",
                metadata: metadata);

            GovernanceDecision decision = await evaluator
                .EvaluateAsync(context, cancellationToken)
                .ConfigureAwait(false);

            IAsiBackboneActorContext actor = AsiBackboneActorContext.Human(
                "external-consumer-user",
                "External Consumer User");

            AuditResidue residue = AuditResidue.FromDecision(
                actor,
                $"external-consumer.{mode}",
                decision,
                metadata: context.Metadata);

            await auditSink.WriteAsync(residue, cancellationToken).ConfigureAwait(false);

            AuditLedgerRecord record = AuditLedgerRecord.FromResidue(residue);
            OperationResult<AuditLedgerRecord> appendResult = await ledgerStore
                .AppendAsync(record, cancellationToken)
                .ConfigureAwait(false);

            if (appendResult.Failed)
            {
                return Results.Problem("The external consumer smoke-test ledger append failed.");
            }

            AuditLedgerRecord? efLedgerRecord = await ledgerStore
                .FindByRecordIdAsync(appendResult.Value.RecordId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new SmokeDecisionResponse(
                Decision: decision.Outcome.ToString(),
                CanProceed: decision.CanProceed,
                RequiresAcknowledgment: decision.RequiresAcknowledgment,
                ReasonCodes: [.. decision.ReasonCodes],
                CorrelationId: correlationId,
                AuditEventId: residue.EventId,
                LedgerRecordId: appendResult.Value.RecordId,
                EfLedgerRecordCount: efLedgerRecord is null ? 0 : 1));
        });

        return app;
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(string mode)
    {
        return mode.ToUpperInvariant() switch
        {
            "ALLOW" => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = "US-LA",
                ["risk"] = "routine",
                ["path"] = "in-memory-and-ef"
            },
            "ACK" => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = "US-LA",
                ["risk"] = "consequential",
                ["path"] = "in-memory-and-ef"
            },
            _ => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["risk"] = "routine",
                ["path"] = "in-memory-and-ef"
            }
        };
    }
}

internal sealed class SmokeHostDbContext(DbContextOptions<SmokeHostDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        _ = modelBuilder.ApplyAsiBackboneConfigurations();
    }
}

internal sealed class SmokeRegionConstraint : IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>
{
    public string Name => "smoke.region";

    public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        AsiBackboneConstraintEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool hasRegion = context.Metadata.TryGetValue("region", out string? region)
            && !string.IsNullOrWhiteSpace(region);

        return ValueTask.FromResult(hasRegion
            ? ConstraintEvaluationResult.Allow()
            : ConstraintEvaluationResult.Deny(
                "smoke.region.missing",
                "The external consumer host requires region metadata before execution."));
    }
}

internal sealed class SmokeDecisionPolicy : IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>
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

        bool requiresAcknowledgment = context.Metadata.TryGetValue("risk", out string? risk)
            && string.Equals(risk, "consequential", StringComparison.OrdinalIgnoreCase);

        return ValueTask.FromResult(requiresAcknowledgment
            ? GovernanceDecision.RequireAcknowledgment(
                "smoke.acknowledgment.required",
                "The external consumer host requires acknowledgment before execution.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)
            : composedDecision);
    }
}

internal sealed record SmokeRegistrationResponse(
    bool AspNetCoreAdapterResolved,
    bool HostOwnedDbContextResolved,
    bool EfLedgerStoreResolved,
    bool InMemoryLedgerResolved);

internal sealed record SmokeDecisionResponse(
    string Decision,
    bool CanProceed,
    bool RequiresAcknowledgment,
    string[] ReasonCodes,
    string CorrelationId,
    string AuditEventId,
    string LedgerRecordId,
    int EfLedgerRecordCount);
CSHARP

rm "$smoke_project_dir/UnitTest1.cs"

dotnet test --project "$smoke_project" --configuration "$configuration" --verbosity normal

echo "External consumer smoke test completed successfully."

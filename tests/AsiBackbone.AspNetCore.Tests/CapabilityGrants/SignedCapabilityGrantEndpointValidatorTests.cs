using System.Buffers.Text;
using System.Text;
using System.Text.Json.Nodes;
using AsiBackbone.AspNetCore.CapabilityGrants;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Signing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;
using Host = AsiBackbone.AspNetCore.Tests.CapabilityGrants.SignedCapabilityGrantTestHost;

namespace AsiBackbone.AspNetCore.Tests.CapabilityGrants;

/// <summary>
/// Tests for <see cref="SignedCapabilityGrantEndpointValidator" /> on its own and through
/// <see cref="DefaultEndpointGovernanceService" />.
/// </summary>
public sealed class SignedCapabilityGrantEndpointValidatorTests
{
    /// <summary>
    /// Provides every policy action a core validator may attach to a failed result.
    /// </summary>
    public static TheoryData<VerificationPolicyAction> FailedResultActions =>
    [
        VerificationPolicyAction.Deny,
        VerificationPolicyAction.Defer,
        VerificationPolicyAction.Escalate,
        VerificationPolicyAction.RequireAcknowledgment,
        VerificationPolicyAction.Allow
    ];

    /// <summary>
    /// Provides every validation category other than <see cref="CapabilityGrantValidationCategory.Valid" />.
    /// </summary>
    public static TheoryData<CapabilityGrantValidationCategory> FailureCategories
    {
        get
        {
            TheoryData<CapabilityGrantValidationCategory> data = [];
            foreach (CapabilityGrantValidationCategory category in Enum.GetValues<CapabilityGrantValidationCategory>())
            {
                if (category is not CapabilityGrantValidationCategory.Valid)
                {
                    data.Add(category);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Provides decision outcomes a valid grant must preserve.
    /// </summary>
    public static TheoryData<GovernanceDecisionOutcome> PreservedOutcomes =>
    [
        GovernanceDecisionOutcome.Allowed,
        GovernanceDecisionOutcome.Warning,
        GovernanceDecisionOutcome.AcknowledgmentRequired,
        GovernanceDecisionOutcome.Deferred,
        GovernanceDecisionOutcome.EscalationRecommended
    ];

    /// <summary>
    /// Verifies every failure category denies, whatever action the core validator attached, with the documented reason code.
    /// </summary>
    /// <param name="category">The failure category.</param>
    [Theory]
    [MemberData(nameof(FailureCategories))]
    public void EveryFailureCategoryDeniesRegardlessOfAction(CapabilityGrantValidationCategory category)
    {
        var current = GovernanceDecision.RequireAcknowledgment(
            "ack.required",
            "Acknowledgment is required.",
            correlationId: "correlation-1",
            policyVersion: Host.PolicyVersion,
            policyHash: Host.PolicyHash);

        foreach (VerificationPolicyAction action in FailedResultActions)
        {
            var result = CapabilityGrantValidationResult.Failed(
                Host.CreateGrant(),
                category,
                action,
                "capability.test-failure");

            GovernanceDecision? decision = SignedCapabilityGrantEndpointValidator.MapRejection(current, result);

            Assert.NotNull(decision);
            Assert.True(decision.IsDenied);
            Assert.Equal([SignedCapabilityGrantReasonCodes.ForCategory(category)], decision.ReasonCodes);
            Assert.Equal("correlation-1", decision.CorrelationId);
            Assert.Equal(Host.PolicyVersion, decision.PolicyVersion);
        }
    }

    /// <summary>
    /// Verifies a valid result maps to no rejection.
    /// </summary>
    [Fact]
    public void ValidResultIsNotRejected()
    {
        Assert.Null(SignedCapabilityGrantEndpointValidator.MapRejection(
            GovernanceDecision.Allow(),
            CapabilityGrantValidationResult.Valid(Host.CreateGrant())));
    }

    /// <summary>
    /// Verifies every failure category has a distinct, documented reason code.
    /// </summary>
    [Fact]
    public void ReasonCodesAreStableAndDistinctPerCategory()
    {
        Dictionary<string, CapabilityGrantValidationCategory> seen = new(StringComparer.Ordinal);

        foreach (CapabilityGrantValidationCategory category in Enum.GetValues<CapabilityGrantValidationCategory>())
        {
            string code = SignedCapabilityGrantReasonCodes.ForCategory(category);
            Assert.StartsWith("endpoint.capability_grant.", code, StringComparison.Ordinal);

            if (category is CapabilityGrantValidationCategory.Valid or CapabilityGrantValidationCategory.Failed)
            {
                Assert.Equal(SignedCapabilityGrantReasonCodes.Failed, code);
                continue;
            }

            Assert.True(seen.TryAdd(code, category), $"{category} reuses the reason code of {seen.GetValueOrDefault(code)}.");
        }

        Assert.Equal(SignedCapabilityGrantReasonCodes.Failed, SignedCapabilityGrantReasonCodes.ForCategory((CapabilityGrantValidationCategory)999));
        Assert.Equal("endpoint.capability_grant.not_yet_valid", SignedCapabilityGrantReasonCodes.NotYetValid);
        Assert.Equal("endpoint.capability_grant.use_limit_exceeded", SignedCapabilityGrantReasonCodes.UseLimitExceeded);
    }

    /// <summary>
    /// Verifies a valid grant returns the current decision unchanged for every non-denied outcome and records the grant.
    /// </summary>
    /// <param name="outcome">The current decision outcome.</param>
    [Theory]
    [MemberData(nameof(PreservedOutcomes))]
    public async Task ValidGrantPreservesCurrentDecision(GovernanceDecisionOutcome outcome)
    {
        using var host = new Host();
        CapabilityGrant grant = Host.CreateGrant();
        string token = await host.IssueTokenAsync(grant);
        using IServiceScope scope = host.Services.CreateScope();
        DefaultHttpContext httpContext = Host.CreateHttpContext(scope.ServiceProvider, token);
        GovernanceDecision current = CreateDecision(outcome);

        GovernanceDecision decision = await ValidateAsync(scope, httpContext, current);

        Assert.Same(current, decision);
        Assert.Equal(grant.TokenId, httpContext.Features.Get<IEndpointCapabilityGrantFeature>()?.GrantId);
        Assert.Equal(outcome is GovernanceDecisionOutcome.AcknowledgmentRequired ? 0 : 1, host.UseStore.TotalUses);
    }

    /// <summary>
    /// Verifies a single-use grant survives an acknowledgment challenge and is consumed by the acknowledged retry.
    /// </summary>
    [Fact]
    public async Task SingleUseGrantSurvivesAcknowledgmentChallengeAndIsConsumedOnRetry()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();
        GovernanceDecision challenge = CreateDecision(GovernanceDecisionOutcome.AcknowledgmentRequired);

        GovernanceDecision challenged = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), challenge);
        GovernanceDecision retried = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());
        GovernanceDecision replayed = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());

        Assert.Same(challenge, challenged);
        Assert.True(retried.IsAllowed, string.Join(",", retried.ReasonCodes));
        Assert.Equal([SignedCapabilityGrantReasonCodes.UseLimitExceeded], replayed.ReasonCodes);
    }

    /// <summary>
    /// Verifies an acknowledgment-required request still fully validates the grant even though no use is consumed.
    /// </summary>
    [Fact]
    public async Task AcknowledgmentRequiredRequestStillValidatesGrant()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync(Host.CreateGrant(subjectId: "user-other"));
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision decision = await ValidateAsync(
            scope,
            Host.CreateHttpContext(scope.ServiceProvider, token),
            CreateDecision(GovernanceDecisionOutcome.AcknowledgmentRequired));

        Assert.Equal([SignedCapabilityGrantReasonCodes.SubjectMismatch], decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies an existing denial is returned unchanged without reading or consuming the grant.
    /// </summary>
    [Fact]
    public async Task ExistingDenialIsPreservedWithoutConsumingGrant()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();
        var denied = GovernanceDecision.Deny("policy.denied", "Denied by policy.");

        GovernanceDecision decision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), denied);
        GovernanceDecision later = await ValidateAsync(
            scope,
            Host.CreateHttpContext(scope.ServiceProvider, token),
            GovernanceDecision.Allow(policyVersion: Host.PolicyVersion, policyHash: Host.PolicyHash));

        Assert.Same(denied, decision);
        Assert.True(later.IsAllowed);
        Assert.Equal(1, host.UseStore.TotalUses);
    }

    /// <summary>
    /// Verifies a grant presented by another actor is denied.
    /// </summary>
    [Fact]
    public async Task GrantPresentedByAnotherActorIsDenied()
    {
        await AssertDeniedAsync(
            SignedCapabilityGrantReasonCodes.SubjectMismatch,
            grant: Host.CreateGrant(subjectId: "user-other"));
    }

    /// <summary>
    /// Verifies a grant issued for another operation is denied.
    /// </summary>
    [Fact]
    public async Task GrantForAnotherOperationIsDenied()
    {
        await AssertDeniedAsync(
            SignedCapabilityGrantReasonCodes.OperationMismatch,
            grant: Host.CreateGrant(operationName: "orders.delete"));
    }

    /// <summary>
    /// Verifies a grant bound to other form values is denied.
    /// </summary>
    [Fact]
    public async Task GrantWithAnotherRequestBindingIsDenied()
    {
        await AssertDeniedAsync(
            SignedCapabilityGrantReasonCodes.ResourceMismatch,
            fields: new Dictionary<string, StringValues>(StringComparer.Ordinal)
            {
                ["orderId"] = "order-43",
                ["amount"] = "100.00"
            });
    }

    /// <summary>
    /// Verifies an expired grant is denied.
    /// </summary>
    [Fact]
    public async Task ExpiredGrantIsDenied()
    {
        await AssertDeniedAsync(
            SignedCapabilityGrantReasonCodes.Expired,
            grant: Host.CreateGrant(notBeforeUtc: Host.Now.AddMinutes(-10), expiresUtc: Host.Now.AddSeconds(-1)));
    }

    /// <summary>
    /// Verifies a grant presented before its not-before time is denied rather than deferred.
    /// </summary>
    [Fact]
    public async Task NotYetValidGrantIsDeniedRatherThanDeferred()
    {
        await AssertDeniedAsync(
            SignedCapabilityGrantReasonCodes.NotYetValid,
            grant: Host.CreateGrant(notBeforeUtc: Host.Now.AddMinutes(1)));
    }

    /// <summary>
    /// Verifies the configured clock skew admits a grant whose not-before time is just ahead of the clock.
    /// </summary>
    [Fact]
    public async Task AllowedClockSkewAdmitsGrantJustBeforeNotBefore()
    {
        using var host = new Host(configure: options => options.AllowedClockSkew = TimeSpan.FromSeconds(30));
        string token = await host.IssueTokenAsync(Host.CreateGrant(notBeforeUtc: Host.Now.AddSeconds(10)));
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision decision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());

        Assert.True(decision.IsAllowed, string.Join(",", decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies a grant presented a second time is denied as a replay.
    /// </summary>
    [Fact]
    public async Task GrantPresentedTwiceIsDenied()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision first = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());
        GovernanceDecision second = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());

        Assert.True(first.IsAllowed);
        Assert.True(second.IsDenied);
        Assert.Equal([SignedCapabilityGrantReasonCodes.UseLimitExceeded], second.ReasonCodes);
    }

    /// <summary>
    /// Verifies issuer, audience, scope, and policy expectations are enforced.
    /// </summary>
    [Fact]
    public async Task IssuerAudienceScopeAndPolicyAreEnforced()
    {
        await AssertDeniedAsync(SignedCapabilityGrantReasonCodes.IssuerMismatch, grant: Host.CreateGrant(issuer: "https://other.test"));
        await AssertDeniedAsync(SignedCapabilityGrantReasonCodes.AudienceMismatch, grant: Host.CreateGrant(audience: "other-api"));
        await AssertDeniedAsync(SignedCapabilityGrantReasonCodes.ScopeMissing, grant: Host.CreateGrant(scopes: ["orders.read"]));
        await AssertDeniedAsync(SignedCapabilityGrantReasonCodes.PolicyMismatch, grant: Host.CreateGrant(policyVersion: "policy-v0"));
    }

    /// <summary>
    /// Verifies policy binding can be turned off by the host.
    /// </summary>
    [Fact]
    public async Task PolicyBindingCanBeDisabled()
    {
        using var host = new Host(configure: options => options.BindPolicy = false);
        string token = await host.IssueTokenAsync(Host.CreateGrant(policyVersion: "policy-v0"));
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision decision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());

        Assert.True(decision.IsAllowed, string.Join(",", decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies a token edited after signing is denied as invalid proof.
    /// </summary>
    [Fact]
    public async Task TamperedTokenIsDenied()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        JsonObject document = JsonNode.Parse(Base64Url.DecodeFromChars(token.AsSpan(CapabilityGrantToken.FormatPrefix.Length)))!.AsObject();
        document["grant"]!["maxUseCount"] = 50;
        string forgedToken = CapabilityGrantToken.FormatPrefix + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(document.ToJsonString()));

        using IServiceScope scope = host.Services.CreateScope();
        GovernanceDecision decision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, forgedToken), AllowedDecision());

        Assert.Equal([SignedCapabilityGrantReasonCodes.ProofInvalid], decision.ReasonCodes);
        Assert.Equal(0, host.UseStore.TotalUses);
    }

    /// <summary>
    /// Verifies missing, malformed, and ambiguous tokens are denied.
    /// </summary>
    [Fact]
    public async Task MissingMalformedAndAmbiguousTokensAreDenied()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision missing = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider), AllowedDecision());
        GovernanceDecision malformed = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, "asibcg1.###"), AllowedDecision());
        DefaultHttpContext ambiguousContext = Host.CreateHttpContext(scope.ServiceProvider);
        ambiguousContext.Request.Headers[SignedCapabilityGrantOptions.DefaultTokenHeaderName] = new StringValues([token, token]);
        GovernanceDecision ambiguous = await ValidateAsync(scope, ambiguousContext, AllowedDecision());

        Assert.Equal([SignedCapabilityGrantReasonCodes.Missing], missing.ReasonCodes);
        Assert.Equal([SignedCapabilityGrantReasonCodes.Malformed], malformed.ReasonCodes);
        Assert.Equal([SignedCapabilityGrantReasonCodes.Malformed], ambiguous.ReasonCodes);
        Assert.Equal(0, host.UseStore.TotalUses);
    }

    /// <summary>
    /// Verifies an unauthenticated request is denied before the grant is read.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedActorIsDenied()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision decision = await ValidateAsync(
            scope,
            Host.CreateHttpContext(scope.ServiceProvider, token, authenticated: false),
            AllowedDecision());

        Assert.Equal([SignedCapabilityGrantReasonCodes.ActorUnresolved], decision.ReasonCodes);
        Assert.Equal(0, host.UseStore.TotalUses);
    }

    /// <summary>
    /// Verifies a missing verifier, use store, or binding provider denies rather than weakening validation.
    /// </summary>
    [Fact]
    public async Task MissingHostServicesDeny()
    {
        await AssertDeniedAsync(SignedCapabilityGrantReasonCodes.ProofMissing, registerVerifier: false);
        await AssertDeniedAsync(SignedCapabilityGrantReasonCodes.UseStoreUnavailable, registerUseStore: false);
        await AssertDeniedAsync(SignedCapabilityGrantReasonCodes.BindingUnavailable, registerBinding: false);
    }

    /// <summary>
    /// Verifies use store states other than accepted, including unspecified and unavailable, deny.
    /// </summary>
    [Fact]
    public async Task UseStoreFailuresDeny()
    {
        await AssertUseStoreStateAsync(GrantUseState.Unspecified, SignedCapabilityGrantReasonCodes.Failed);
        await AssertUseStoreStateAsync(GrantUseState.Unavailable, SignedCapabilityGrantReasonCodes.UseStoreUnavailable);
        await AssertUseStoreStateAsync(GrantUseState.Stopped, SignedCapabilityGrantReasonCodes.Revoked);
        await AssertUseStoreStateAsync(GrantUseState.Cancelled, SignedCapabilityGrantReasonCodes.Cancelled);

        using var host = new Host();
        host.UseStore.Throw = true;
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();
        GovernanceDecision decision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());

        Assert.Equal([SignedCapabilityGrantReasonCodes.Failed], decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies verifier failure categories deny with their mapped reason codes.
    /// </summary>
    [Fact]
    public async Task VerifierFailuresDeny()
    {
        await AssertVerifierFailureAsync(SignatureVerificationCategory.RevokedKey, SignedCapabilityGrantReasonCodes.Revoked);
        await AssertVerifierFailureAsync(SignatureVerificationCategory.ProviderUnavailable, SignedCapabilityGrantReasonCodes.Failed);
        await AssertVerifierFailureAsync(SignatureVerificationCategory.UntrustedKey, SignedCapabilityGrantReasonCodes.ProofInvalid);
    }

    /// <summary>
    /// Verifies a grant bound to a resource is denied when the request binds no resource.
    /// </summary>
    [Fact]
    public async Task ResourceBoundGrantIsDeniedWhenRequestHasNoBinding()
    {
        using var host = new Host(
            registerBinding: false,
            customize: services => services.AddSingleton<ICapabilityGrantRequestBindingProvider>(new NoBindingProvider()));
        string bound = await host.IssueTokenAsync();
        string unbound = await host.IssueTokenAsync(Host.CreateGrant(noResourceBinding: true));
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision boundDecision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, bound), AllowedDecision());
        GovernanceDecision unboundDecision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, unbound), AllowedDecision());

        Assert.Equal([SignedCapabilityGrantReasonCodes.ResourceMismatch], boundDecision.ReasonCodes);
        Assert.True(unboundDecision.IsAllowed, string.Join(",", unboundDecision.ReasonCodes));
    }

    /// <summary>
    /// Verifies a form body carrying files is not bound.
    /// </summary>
    [Fact]
    public async Task FormWithFilesIsNotBound()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();
        DefaultHttpContext httpContext = Host.CreateHttpContext(scope.ServiceProvider, token);
        httpContext.Request.Form = new FormCollection(
            new Dictionary<string, StringValues>(Host.DefaultFields, StringComparer.OrdinalIgnoreCase),
            new FormFileCollection { new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "upload", "upload.bin") });

        GovernanceDecision decision = await ValidateAsync(scope, httpContext, AllowedDecision());

        Assert.Equal([SignedCapabilityGrantReasonCodes.BindingUnavailable], decision.ReasonCodes);
        Assert.Equal(0, host.UseStore.TotalUses);
    }

    /// <summary>
    /// Verifies a non-form body is not bound by the form-field provider.
    /// </summary>
    [Fact]
    public async Task NonFormBodyIsNotBound()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();
        DefaultHttpContext httpContext = Host.CreateHttpContext(scope.ServiceProvider, token);
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Body = new MemoryStream("{}"u8.ToArray());
        httpContext.Request.ContentLength = 2;

        // The helper pre-populates the form; a fresh form feature reflects a request that only carries the JSON body.
        httpContext.Features.Set<IFormFeature>(new FormFeature(httpContext.Request));

        GovernanceDecision decision = await ValidateAsync(scope, httpContext, AllowedDecision());

        Assert.Equal([SignedCapabilityGrantReasonCodes.BindingUnavailable], decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies a token carried in a configured form field is read and kept out of the form binding.
    /// </summary>
    [Fact]
    public async Task TokenCanBeCarriedInFormField()
    {
        using var host = new Host(configure: options =>
        {
            options.TokenHeaderName = null;
            options.TokenFormFieldName = "capability_grant";
        });
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();
        Dictionary<string, StringValues> fields = new(Host.DefaultFields, StringComparer.Ordinal)
        {
            ["capability_grant"] = token,
            ["__RequestVerificationToken"] = "antiforgery-value"
        };

        GovernanceDecision decision = await ValidateAsync(
            scope,
            Host.CreateHttpContext(scope.ServiceProvider, fields: fields),
            AllowedDecision());

        Assert.True(decision.IsAllowed, string.Join(",", decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies an allowed request authorized by a grant records the grant identifier in the receipt and the ledger
    /// record, and the ledger record hash verifies.
    /// </summary>
    [Fact]
    public async Task AllowedRequestRecordsGrantIdentifierInAuditLedgerRecord()
    {
        using var host = new Host();
        CapabilityGrant grant = Host.CreateGrant();
        string token = await host.IssueTokenAsync(grant);
        using IServiceScope scope = host.Services.CreateScope();
        DefaultHttpContext httpContext = Host.CreateHttpContext(scope.ServiceProvider, token);

        EndpointGovernanceResult result = await EvaluateAsync(scope, httpContext);

        Assert.True(result.CanExecute);
        IDecisionReceipt receipt = Assert.Single(host.Sink.Receipts);
        Assert.Equal(grant.TokenId, receipt.CapabilityGrantId);

        var record = AuditLedgerRecord.FromDecisionReceipt(receipt, recordId: "record-1", recordedUtc: Host.Now);
        string recordHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(record)).HashValue;
        var stored = AuditLedgerRecord.FromDecisionReceipt(receipt, recordId: "record-1", recordedUtc: Host.Now, recordHash: recordHash);
        SignedGovernanceArtifact<AuditLedgerRecord> rehydrated = SignedGovernanceArtifacts.Rehydrate(
            stored,
            CanonicalPayloadBuilder.ForAuditLedgerRecord(stored),
            CanonicalPayloadHash.Create(
                CanonicalArtifactTypes.AuditLedgerRecord,
                stored.RecordId,
                stored.SchemaVersion,
                CanonicalPayloadOptions.DefaultCanonicalizationVersion,
                CanonicalPayloadOptions.DefaultHashAlgorithm,
                stored.RecordHash!),
            SigningMetadata.NoSignature);

        Assert.Equal(grant.TokenId, record.CapabilityTokenId);
        Assert.Equal(recordHash, rehydrated.SigningHash);
    }

    /// <summary>
    /// Verifies a denied request does not record a grant identifier.
    /// </summary>
    [Fact]
    public async Task DeniedRequestDoesNotRecordGrantIdentifier()
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync(Host.CreateGrant(subjectId: "user-other"));
        using IServiceScope scope = host.Services.CreateScope();

        EndpointGovernanceResult result = await EvaluateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token));

        Assert.False(result.CanExecute);
        IDecisionReceipt receipt = Assert.Single(host.Sink.Receipts);
        Assert.Null(receipt.CapabilityGrantId);
        Assert.Contains(SignedCapabilityGrantReasonCodes.SubjectMismatch, receipt.ReasonCodes);
    }

    /// <summary>
    /// Verifies a stale grant feature left on the request does not reach the receipt when no grant is accepted.
    /// </summary>
    [Fact]
    public async Task StaleGrantFeatureIsClearedBeforeValidation()
    {
        using var host = new Host();
        using IServiceScope scope = host.Services.CreateScope();
        DefaultHttpContext httpContext = Host.CreateHttpContext(scope.ServiceProvider);
        httpContext.Features.Set<IEndpointCapabilityGrantFeature>(new EndpointCapabilityGrantFeature("stale-grant"));

        EndpointGovernanceResult result = await EvaluateAsync(scope, httpContext);

        Assert.False(result.CanExecute);
        Assert.Null(httpContext.Features.Get<IEndpointCapabilityGrantFeature>());
        Assert.Null(Assert.Single(host.Sink.Receipts).CapabilityGrantId);
    }

    /// <summary>
    /// Verifies a valid grant does not bypass an acknowledgment challenge.
    /// </summary>
    [Fact]
    public async Task ValidGrantDoesNotBypassAcknowledgmentChallenge()
    {
        using var host = new Host(evaluate: context => GovernanceDecision.RequireAcknowledgment(
            "ack.required",
            "Acknowledgment is required.",
            correlationId: context.CorrelationId,
            policyVersion: context.PolicyVersion,
            policyHash: context.PolicyHash));
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();

        EndpointGovernanceResult result = await EvaluateAsync(
            scope,
            Host.CreateHttpContext(scope.ServiceProvider, token),
            new RequireAcknowledgmentAttribute());

        Assert.False(result.CanExecute);
        Assert.NotNull(result.AcknowledgmentChallenge);
        Assert.True(result.Decision!.RequiresAcknowledgment);
    }

    /// <summary>
    /// Verifies a missing grant on an acknowledgment-required endpoint is denied instead of challenged.
    /// </summary>
    [Fact]
    public async Task MissingGrantIsDeniedInsteadOfChallenged()
    {
        using var host = new Host(evaluate: context => GovernanceDecision.RequireAcknowledgment(
            "ack.required",
            "Acknowledgment is required.",
            correlationId: context.CorrelationId,
            policyVersion: context.PolicyVersion,
            policyHash: context.PolicyHash));
        using IServiceScope scope = host.Services.CreateScope();

        EndpointGovernanceResult result = await EvaluateAsync(
            scope,
            Host.CreateHttpContext(scope.ServiceProvider),
            new RequireAcknowledgmentAttribute());

        Assert.False(result.CanExecute);
        Assert.Null(result.AcknowledgmentChallenge);
        Assert.True(result.Decision!.IsDenied);
    }

    /// <summary>
    /// Verifies hosts that do not opt in see no validator registration.
    /// </summary>
    [Fact]
    public void HostsThatDoNotOptInSeeNoRegistrationChange()
    {
        ServiceCollection services = new();
        _ = services.AddAsiBackboneAspNetCore();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IEndpointCapabilityGrantValidator));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ICapabilityGrantRequestBindingProvider));
    }

    /// <summary>
    /// Verifies registration is idempotent, refuses a conflicting validator, and validates options.
    /// </summary>
    [Fact]
    public void RegistrationIsExplicitAndValidated()
    {
        ServiceCollection services = new();
        _ = services.AddAsiBackboneAspNetCore();
        _ = services.AddAsiBackboneSignedCapabilityGrants(options => options.Issuer = "issuer-only");
        _ = services.AddAsiBackboneSignedCapabilityGrants(options => options.Audience = "audience");

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IEndpointCapabilityGrantValidator));

        ServiceCollection invalid = new();
        _ = invalid.AddAsiBackboneSignedCapabilityGrants(options => options.Issuer = "issuer-only");
        using ServiceProvider provider = invalid.BuildServiceProvider();
        _ = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SignedCapabilityGrantOptions>>().Value);

        ServiceCollection conflicting = new();
        _ = conflicting.AddScoped<IEndpointCapabilityGrantValidator, NoOpValidator>();
        _ = Assert.Throws<InvalidOperationException>(() => conflicting.AddAsiBackboneSignedCapabilityGrants(_ => { }));
        _ = Assert.Throws<ArgumentNullException>(() => conflicting.AddAsiBackboneSignedCapabilityGrants(null!));
    }

    /// <summary>
    /// Verifies option validation rejects each invalid setting.
    /// </summary>
    [Fact]
    public void OptionsValidationRejectsInvalidSettings()
    {
        static SignedCapabilityGrantOptions Valid()
        {
            return new() { Issuer = "issuer", Audience = "audience" };
        }

        Valid().Validate();
        AssertInvalid(options => options.Issuer = " ");
        AssertInvalid(options => options.Audience = null);
        AssertInvalid(options => options.AllowedClockSkew = TimeSpan.FromSeconds(-1));
        AssertInvalid(options => options.MaxUseCount = 0);
        AssertInvalid(options => options.MaxTokenLength = 0);
        AssertInvalid(options =>
        {
            options.TokenHeaderName = null;
            options.TokenFormFieldName = null;
        });

        static void AssertInvalid(Action<SignedCapabilityGrantOptions> configure)
        {
            SignedCapabilityGrantOptions options = Valid();
            configure(options);
            _ = Assert.Throws<InvalidOperationException>(options.Validate);
        }
    }

    private static async Task AssertDeniedAsync(
        string expectedReasonCode,
        CapabilityGrant? grant = null,
        IReadOnlyDictionary<string, StringValues>? fields = null,
        bool registerVerifier = true,
        bool registerUseStore = true,
        bool registerBinding = true)
    {
        using var host = new Host(
            registerVerifier: registerVerifier,
            registerUseStore: registerUseStore,
            registerBinding: registerBinding);
        string token = await host.IssueTokenAsync(grant);
        using IServiceScope scope = host.Services.CreateScope();
        DefaultHttpContext httpContext = Host.CreateHttpContext(scope.ServiceProvider, token, fields);

        GovernanceDecision decision = await ValidateAsync(scope, httpContext, AllowedDecision());

        Assert.True(decision.IsDenied, $"Expected {expectedReasonCode} but the decision was {decision.Outcome}.");
        Assert.Equal([expectedReasonCode], decision.ReasonCodes);
        Assert.Null(httpContext.Features.Get<IEndpointCapabilityGrantFeature>());
    }

    private static async Task AssertUseStoreStateAsync(GrantUseState state, string expectedReasonCode)
    {
        using var host = new Host();
        host.UseStore.ForcedState = state;
        string token = await host.IssueTokenAsync();
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision decision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());

        Assert.True(decision.IsDenied);
        Assert.Equal([expectedReasonCode], decision.ReasonCodes);
    }

    private static async Task AssertVerifierFailureAsync(SignatureVerificationCategory category, string expectedReasonCode)
    {
        using var host = new Host();
        string token = await host.IssueTokenAsync();
        host.Signer.ForcedFailure = category;
        using IServiceScope scope = host.Services.CreateScope();

        GovernanceDecision decision = await ValidateAsync(scope, Host.CreateHttpContext(scope.ServiceProvider, token), AllowedDecision());

        Assert.True(decision.IsDenied);
        Assert.Equal([expectedReasonCode], decision.ReasonCodes);
        Assert.Equal(0, host.UseStore.TotalUses);
    }

    private static ValueTask<GovernanceDecision> ValidateAsync(
        IServiceScope scope,
        HttpContext httpContext,
        GovernanceDecision currentDecision)
    {
        IEndpointCapabilityGrantValidator validator = scope.ServiceProvider.GetRequiredService<IEndpointCapabilityGrantValidator>();
        Assert.IsType<SignedCapabilityGrantEndpointValidator>(validator);

        return validator.ValidateAsync(
            httpContext,
            EndpointGovernanceDescriptor.FromEndpoint(Host.CreateEndpoint()),
            currentDecision,
            TestContext.Current.CancellationToken);
    }

    private static ValueTask<EndpointGovernanceResult> EvaluateAsync(
        IServiceScope scope,
        HttpContext httpContext,
        params object[] extraMetadata)
    {
        Endpoint endpoint = Host.CreateEndpoint(extraMetadata);
        httpContext.SetEndpoint(endpoint);

        return scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>().EvaluateAsync(
            httpContext,
            EndpointGovernanceDescriptor.FromEndpoint(endpoint),
            TestContext.Current.CancellationToken);
    }

    private static GovernanceDecision AllowedDecision()
    {
        return GovernanceDecision.Allow(policyVersion: Host.PolicyVersion, policyHash: Host.PolicyHash);
    }

    private static GovernanceDecision CreateDecision(GovernanceDecisionOutcome outcome)
    {
        return outcome switch
        {
            GovernanceDecisionOutcome.Allowed => AllowedDecision(),
            GovernanceDecisionOutcome.Warning => GovernanceDecision.Warning("w", "Warning.", policyVersion: Host.PolicyVersion, policyHash: Host.PolicyHash),
            GovernanceDecisionOutcome.AcknowledgmentRequired => GovernanceDecision.RequireAcknowledgment("a", "Acknowledge.", policyVersion: Host.PolicyVersion, policyHash: Host.PolicyHash),
            GovernanceDecisionOutcome.Deferred => GovernanceDecision.Defer("d", "Deferred.", policyVersion: Host.PolicyVersion, policyHash: Host.PolicyHash),
            GovernanceDecisionOutcome.EscalationRecommended => GovernanceDecision.Escalate("e", "Escalate.", policyVersion: Host.PolicyVersion, policyHash: Host.PolicyHash),
            GovernanceDecisionOutcome.Denied => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Covered separately."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unsupported outcome.")
        };
    }

    private sealed class NoBindingProvider : ICapabilityGrantRequestBindingProvider
    {
        public ValueTask<CapabilityGrantRequestBinding> ResolveAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CapabilityGrantRequestBinding.None);
        }
    }

    private sealed class NoOpValidator : IEndpointCapabilityGrantValidator
    {
        public ValueTask<GovernanceDecision> ValidateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            GovernanceDecision currentDecision,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(currentDecision);
        }
    }
}

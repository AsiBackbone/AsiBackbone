using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.Core.Constraints;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Correlation;

/// <summary>
/// Branch coverage tests for <see cref="GovernanceHttpRequestCorrelation"/> metadata normalization and merging.
/// </summary>
public sealed class GovernanceHttpRequestCorrelationBranchTests
{
    /// <summary>
    /// Verifies that request metadata containing only blank keys normalizes to an empty collection and null values become empty strings.
    /// </summary>
    [Fact]
    public void ConstructorDropsBlankKeysAndNormalizesNullValues()
    {
        var blankOnly = new GovernanceHttpRequestCorrelation(metadata: new Dictionary<string, string> { [" "] = "ignored" });
        var withNullValue = new GovernanceHttpRequestCorrelation(metadata: new Dictionary<string, string> { [" route "] = null! });

        Assert.False(blankOnly.HasMetadata);
        Assert.Empty(blankOnly.Metadata);
        Assert.Equal(string.Empty, withNullValue.Metadata["route"]);
    }

    /// <summary>
    /// Verifies that merging returns empty metadata when neither the request nor the host supplied any entries.
    /// </summary>
    [Fact]
    public void MergeMetadataReturnsEmptyWhenNoMetadataIsAvailable()
    {
        var correlation = new GovernanceHttpRequestCorrelation("correlation-1");

        Assert.Empty(correlation.MergeMetadata(null));
        Assert.Empty(correlation.MergeMetadata(new Dictionary<string, string>()));
    }

    /// <summary>
    /// Verifies that merging host metadata containing only blank keys into empty request metadata yields empty metadata.
    /// </summary>
    [Fact]
    public void MergeMetadataReturnsEmptyWhenOnlyBlankHostKeysAreSupplied()
    {
        var correlation = new GovernanceHttpRequestCorrelation("correlation-1");

        IReadOnlyDictionary<string, string> merged = correlation.MergeMetadata(new Dictionary<string, string> { ["  "] = "ignored" });

        Assert.Empty(merged);
    }

    /// <summary>
    /// Verifies that merging request metadata without host metadata keeps the request entries.
    /// </summary>
    [Fact]
    public void MergeMetadataKeepsRequestMetadataWhenHostMetadataIsNull()
    {
        var correlation = new GovernanceHttpRequestCorrelation(metadata: new Dictionary<string, string> { ["route"] = "/orders" });

        IReadOnlyDictionary<string, string> merged = correlation.MergeMetadata(null);

        Assert.Equal("/orders", merged["route"]);
    }

    /// <summary>
    /// Verifies that evaluation contexts built without request metadata merging normalize only the supplied metadata.
    /// </summary>
    [Fact]
    public void ToEvaluationContextWithoutMergingNormalizesOnlySuppliedMetadata()
    {
        var correlation = new GovernanceHttpRequestCorrelation(
            "correlation-1",
            metadata: new Dictionary<string, string> { ["route"] = "/orders" });

        GovernanceEvaluationContext missing = correlation.ToEvaluationContext(mergeRequestMetadata: false);
        GovernanceEvaluationContext empty = correlation.ToEvaluationContext(
            metadata: new Dictionary<string, string>(),
            mergeRequestMetadata: false);
        GovernanceEvaluationContext blankOnly = correlation.ToEvaluationContext(
            metadata: new Dictionary<string, string> { [" "] = "ignored" },
            mergeRequestMetadata: false);
        GovernanceEvaluationContext supplied = correlation.ToEvaluationContext(
            metadata: new Dictionary<string, string> { [" tenant "] = " tenant-1 " },
            mergeRequestMetadata: false);

        Assert.Empty(missing.Metadata);
        Assert.Empty(empty.Metadata);
        Assert.Empty(blankOnly.Metadata);
        Assert.Equal("tenant-1", supplied.Metadata["tenant"]);
        Assert.False(supplied.Metadata.ContainsKey("route"));
    }

    /// <summary>
    /// Verifies the resolved request trace identifier is carried into the framework-neutral evaluation context.
    /// </summary>
    [Fact]
    public void ToEvaluationContextCarriesTraceIdentifier()
    {
        var correlation = new GovernanceHttpRequestCorrelation(
            correlationId: "correlation-1",
            traceId: " trace-1 ");

        GovernanceEvaluationContext context = correlation.ToEvaluationContext();

        Assert.Equal("trace-1", context.TraceId);
    }
}

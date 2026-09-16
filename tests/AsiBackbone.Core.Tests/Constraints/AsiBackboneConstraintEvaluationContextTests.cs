using AsiBackbone.Core.Constraints;
using Xunit;

namespace AsiBackbone.Core.Tests.Constraints;

/// <summary>
/// Unit tests for <see cref="GovernanceEvaluationContext"/> to verify that it correctly normalizes optional values and metadata.
/// </summary>
public sealed class AsiBackboneConstraintEvaluationContextTests
{
    /// <summary>
    /// Verifies that the default constructor creates an empty context with no optional values or metadata.
    /// </summary>
    [Fact]
    public void ConstructorCreatesEmptyContextByDefault()
    {
        var context = new GovernanceEvaluationContext();

        Assert.Null(context.CorrelationId);
        Assert.Null(context.PolicyVersion);
        Assert.Null(context.PolicyHash);
        Assert.False(context.HasMetadata);
        Assert.Empty(context.Metadata);
    }

    /// <summary>
    /// Verifies that the constructor of <see cref="GovernanceEvaluationContext"/> correctly normalizes optional values by trimming whitespace and converting empty strings to null.
    /// </summary>
    [Fact]
    public void ConstructorNormalizesOptionalValues()
    {
        var context = new GovernanceEvaluationContext(
            correlationId: " correlation-123 ",
            policyVersion: " v1 ",
            policyHash: " hash-abc ");

        Assert.Equal("correlation-123", context.CorrelationId);
        Assert.Equal("v1", context.PolicyVersion);
        Assert.Equal("hash-abc", context.PolicyHash);
    }

    /// <summary>
    /// Verifies that the constructor of <see cref="GovernanceEvaluationContext"/> converts whitespace-only strings to null for optional values.
    /// </summary>
    [Fact]
    public void ConstructorConvertsWhitespaceValuesToNull()
    {
        var context = new GovernanceEvaluationContext(
            correlationId: " ",
            policyVersion: "",
            policyHash: null);

        Assert.Null(context.CorrelationId);
        Assert.Null(context.PolicyVersion);
        Assert.Null(context.PolicyHash);
    }

    /// <summary>
    /// Verifies that null metadata is normalized to an empty metadata collection.
    /// </summary>
    [Fact]
    public void ConstructorWithNullMetadataReturnsNoMetadata()
    {
        var context = new GovernanceEvaluationContext(
            metadata: null);

        Assert.False(context.HasMetadata);
        Assert.Empty(context.Metadata);
    }

    /// <summary>
    /// Verifies that empty metadata is normalized to an empty metadata collection.
    /// </summary>
    [Fact]
    public void ConstructorWithEmptyMetadataReturnsNoMetadata()
    {
        var context = new GovernanceEvaluationContext(
            metadata: new Dictionary<string, string>());

        Assert.False(context.HasMetadata);
        Assert.Empty(context.Metadata);
    }

    /// <summary>
    /// Verifies that the constructor of <see cref="GovernanceEvaluationContext"/> correctly normalizes metadata by trimming whitespace and ignoring blank keys.
    /// </summary>
    [Fact]
    public void ConstructorNormalizesMetadata()
    {
        var context = new GovernanceEvaluationContext(
            metadata: new Dictionary<string, string>
            {
                [" region "] = " us-la ",
                [" "] = "ignored",
                ["risk"] = " high "
            });

        Assert.True(context.HasMetadata);
        Assert.Equal(2, context.Metadata.Count);
        Assert.Equal("us-la", context.Metadata["region"]);
        Assert.Equal("high", context.Metadata["risk"]);
    }

    /// <summary>
    /// Verifies that metadata containing only blank keys is normalized to an empty metadata collection.
    /// </summary>
    [Fact]
    public void ConstructorWithOnlyBlankMetadataKeysReturnsNoMetadata()
    {
        var context = new GovernanceEvaluationContext(
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored",
                ["\t"] = "also ignored"
            });

        Assert.False(context.HasMetadata);
        Assert.Empty(context.Metadata);
    }

    /// <summary>
    /// Verifies that duplicate keys after trimming use the later normalized value.
    /// </summary>
    [Fact]
    public void ConstructorWithDuplicateTrimmedMetadataKeysUsesLastValue()
    {
        var context = new GovernanceEvaluationContext(
            metadata: new Dictionary<string, string>
            {
                [" region "] = " us-la ",
                ["region"] = " us-tx "
            });

        Assert.True(context.HasMetadata);
        _ = Assert.Single(context.Metadata);
        Assert.Equal("us-tx", context.Metadata["region"]);
    }

    /// <summary>
    /// Verifies that null metadata values are normalized to empty strings while preserving valid keys.
    /// </summary>
    [Fact]
    public void ConstructorWithNullMetadataValueStoresEmptyString()
    {
        var context = new GovernanceEvaluationContext(
            metadata: new Dictionary<string, string>
            {
                [" source "] = null!
            });

        Assert.True(context.HasMetadata);
        Assert.Equal(string.Empty, context.Metadata["source"]);
    }

    /// <summary>
    /// Verifies that mutating the source metadata after construction does not change the constraint evaluation context metadata.
    /// </summary>
    [Fact]
    public void ConstructorDoesNotAliasSourceMetadata()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [" region "] = " us-la "
        };

        var context = new GovernanceEvaluationContext(
            metadata: metadata);

        metadata[" region "] = " us-tx ";
        metadata[" risk "] = " high ";

        _ = Assert.Single(context.Metadata);
        Assert.Equal("us-la", context.Metadata["region"]);
        Assert.False(context.Metadata.ContainsKey("risk"));
    }

    /// <summary>
    /// Verifies that metadata cannot be mutated through dictionary casts.
    /// </summary>
    [Fact]
    public void MetadataCannotBeMutatedThroughDictionaryCasts()
    {
        var context = new GovernanceEvaluationContext(
            metadata: new Dictionary<string, string>
            {
                [" region "] = " us-la "
            });

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(context.Metadata);

        _ = Assert.Single(context.Metadata);
        Assert.Equal("us-la", context.Metadata["region"]);
    }

    /// <summary>
    /// Verifies that empty metadata cannot be mutated through dictionary casts.
    /// </summary>
    [Fact]
    public void EmptyMetadataCannotBeMutatedThroughDictionaryCasts()
    {
        var context = new GovernanceEvaluationContext();

        ReadOnlyMetadataAssert.CannotMutateThroughCasts(context.Metadata);

        Assert.False(context.HasMetadata);
        Assert.Empty(context.Metadata);
    }
}

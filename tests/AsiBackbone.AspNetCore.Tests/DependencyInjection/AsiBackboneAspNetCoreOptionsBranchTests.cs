using AsiBackbone.AspNetCore.DependencyInjection;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.DependencyInjection;

/// <summary>
/// Tests for the <see cref="AspNetCoreGovernanceOptions"/> class, focusing on the behavior of its properties and validation logic.
/// </summary>
public sealed class AsiBackboneAspNetCoreOptionsBranchTests
{
    /// <summary>
    /// Tests that the <see cref="AspNetCoreGovernanceOptions.IncludeEndpointMetadata"/> property correctly reflects the state of the <see cref="AspNetCoreGovernanceOptions.IncludeEndpointDisplayName"/> and <see cref="AspNetCoreGovernanceOptions.IncludeRoutePattern"/> properties.
    /// </summary>
    [Fact]
    public void IncludeEndpointMetadataGetterReflectsDisplayNameAndRoutePatternFlags()
    {
        var options = new AspNetCoreGovernanceOptions
        {
            IncludeEndpointDisplayName = false,
            IncludeRoutePattern = false,
        };

        Assert.False(options.IncludeEndpointMetadata);

        options.IncludeEndpointDisplayName = true;

        Assert.True(options.IncludeEndpointMetadata);

        options.IncludeEndpointDisplayName = false;
        options.IncludeRoutePattern = true;

        Assert.True(options.IncludeEndpointMetadata);
    }

    /// <summary>
    /// Tests that setting the <see cref="AspNetCoreGovernanceOptions.IncludeEndpointMetadata"/> property updates both the <see cref="AspNetCoreGovernanceOptions.IncludeEndpointDisplayName"/> and <see cref="AspNetCoreGovernanceOptions.IncludeRoutePattern"/> properties accordingly.
    /// </summary>
    [Fact]
    public void IncludeEndpointMetadataSetterUpdatesBothEndpointFlags()
    {
        var options = new AspNetCoreGovernanceOptions
        {
            IncludeEndpointMetadata = false,
        };

        Assert.False(options.IncludeEndpointDisplayName);
        Assert.False(options.IncludeRoutePattern);

        options.IncludeEndpointMetadata = true;

        Assert.True(options.IncludeEndpointDisplayName);
        Assert.True(options.IncludeRoutePattern);
    }

    /// <summary>
    /// Tests that the <see cref="AspNetCoreGovernanceOptions.CorrelationIdHeaderName"/> property returns an empty string when no header names are configured.
    /// </summary>
    [Fact]
    public void CorrelationIdHeaderNameReturnsEmptyWhenNoHeaderNamesAreConfigured()
    {
        var options = new AspNetCoreGovernanceOptions
        {
            CorrelationIdHeaderNames = [],
        };

        Assert.Equal(string.Empty, options.CorrelationIdHeaderName);
    }

    /// <summary>
    /// Tests that setting the <see cref="AspNetCoreGovernanceOptions.CorrelationIdHeaderName"/> property replaces any previously configured header names in the <see cref="AspNetCoreGovernanceOptions.CorrelationIdHeaderNames"/> collection.
    /// </summary>
    [Fact]
    public void CorrelationIdHeaderNameSetterReplacesConfiguredHeaderNames()
    {
        var options = new AspNetCoreGovernanceOptions
        {
            CorrelationIdHeaderNames = ["X-First", "X-Second"],
            CorrelationIdHeaderName = "X-Replacement"
        };

        Assert.Equal("X-Replacement", options.CorrelationIdHeaderName);
        Assert.Equal(["X-Replacement"], options.CorrelationIdHeaderNames);
    }

    /// <summary>
    /// Tests that the <see cref="AspNetCoreGovernanceOptions.Validate"/> method accepts header names when at least one configured header is not blank.
    /// </summary>
    [Fact]
    public void ValidateAcceptsHeaderNamesWhenAtLeastOneConfiguredHeaderIsNotBlank()
    {
        var options = new AspNetCoreGovernanceOptions
        {
            CorrelationIdHeaderNames = [" ", "X-Correlation-ID"],
        };

        Exception? exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that the <see cref="AspNetCoreGovernanceOptions.Validate"/> method throws an <see cref="InvalidOperationException"/> when the <see cref="AspNetCoreGovernanceOptions.CorrelationIdHeaderNames"/> property is set to null.
    /// </summary>
    [Fact]
    public void ValidateRejectsNullCorrelationHeaderNames()
    {
        var options = new AspNetCoreGovernanceOptions
        {
            CorrelationIdHeaderNames = null!,
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("correlation identifier header name", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that the <see cref="AspNetCoreGovernanceOptions.Validate"/> method throws an <see cref="InvalidOperationException"/> when the <see cref="AspNetCoreGovernanceOptions.CorrelationIdHeaderNames"/> property is set to an empty collection.
    /// </summary>
    [Fact]
    public void ValidateRejectsEmptyCorrelationHeaderNames()
    {
        var options = new AspNetCoreGovernanceOptions
        {
            CorrelationIdHeaderNames = [],
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("correlation identifier header name", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that the <see cref="AspNetCoreGovernanceOptions.Validate"/> method throws an <see cref="InvalidOperationException"/> when the <see cref="AspNetCoreGovernanceOptions.CorrelationIdHeaderNames"/> property is set to a collection containing only whitespace.
    /// </summary>
    [Fact]
    public void ValidateRejectsWhitespaceOnlyCorrelationHeaderNames()
    {
        var options = new AspNetCoreGovernanceOptions
        {
            CorrelationIdHeaderNames = [" ", "\t"],
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("correlation identifier header name", exception.Message, StringComparison.Ordinal);
    }
}

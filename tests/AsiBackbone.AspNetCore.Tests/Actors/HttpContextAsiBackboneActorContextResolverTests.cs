#pragma warning disable CS1591

using System.Security.Claims;
using AsiBackbone.AspNetCore.Actors;
using AsiBackbone.Core.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Actors;

/// <summary>
/// Tests for <see cref="HttpContextGovernanceActorContextResolver"/>.
/// </summary>
public sealed class HttpContextAsiBackboneActorContextResolverTests
{
    [Fact]
    public void ResolveActorContextMapsAuthenticatedUserFromDefaultClaims()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            new Claim(ClaimTypes.NameIdentifier, "  user-123  "),
            new Claim(ClaimTypes.Name, "  Ada Lovelace  "));

        IGovernanceActorContext actor = CreateResolver(httpContext).ResolveActorContext();

        Assert.Equal("user-123", actor.ActorId);
        Assert.Equal(GovernanceActorType.Human, actor.ActorType);
        Assert.Equal("Ada Lovelace", actor.DisplayName);
        Assert.True(actor.IsAuthenticated);
    }

    [Fact]
    public void ResolveActorContextAcceptsHumanClaimByDefault()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            new Claim(ClaimTypes.NameIdentifier, "human-1"),
            new Claim("actor_type", "Human"));

        IGovernanceActorContext actor = CreateResolver(httpContext).ResolveActorContext();

        Assert.Equal(GovernanceActorType.Human, actor.ActorType);
        Assert.Equal("human-1", actor.ActorId);
    }

    [Theory]
    [InlineData("System")]
    [InlineData("Service")]
    [InlineData("Agent")]
    [InlineData("Unknown")]
    public void ResolveActorContextRejectsNonHumanClaimsByDefault(string claimedActorType)
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            new Claim(ClaimTypes.NameIdentifier, "caller-1"),
            new Claim("actor_type", claimedActorType));

        IGovernanceActorContext actor = CreateResolver(httpContext).ResolveActorContext();

        Assert.Equal(GovernanceActorType.Human, actor.ActorType);
        Assert.Equal("caller-1", actor.ActorId);
        Assert.True(actor.IsAuthenticated);
    }

    [Fact]
    public void ResolveActorContextAcceptsServiceAfterExplicitOptIn()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            new Claim("custom_id", "service-42"),
            new Claim("custom_name", "Service Worker"),
            new Claim("custom_actor_type", "Service"));

        IGovernanceActorContext actor = CreateResolver(
            httpContext,
            options =>
            {
                options.ActorIdClaimTypes = ["custom_id"];
                options.DisplayNameClaimTypes = ["custom_name"];
                options.ActorTypeClaimType = "custom_actor_type";
                options.AllowedActorTypesFromClaims = [GovernanceActorType.Human, GovernanceActorType.Service];
            }).ResolveActorContext();

        Assert.Equal("service-42", actor.ActorId);
        Assert.Equal(GovernanceActorType.Service, actor.ActorType);
        Assert.Equal("Service Worker", actor.DisplayName);
    }

    [Fact]
    public void ResolveActorContextAcceptsSystemAfterExplicitOptIn()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            new Claim(ClaimTypes.NameIdentifier, "ignored-system-id"),
            new Claim("actor_type", "System"));

        IGovernanceActorContext actor = CreateResolver(
            httpContext,
            options => options.AllowedActorTypesFromClaims = [GovernanceActorType.System]).ResolveActorContext();

        Assert.Equal(GovernanceActorContext.SystemActorId, actor.ActorId);
        Assert.Equal(GovernanceActorType.System, actor.ActorType);
    }

    [Fact]
    public void ResolveActorContextAcceptsAgentAfterExplicitOptIn()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            new Claim(ClaimTypes.NameIdentifier, "agent-007"),
            new Claim(ClaimTypes.Name, "Agent Runner"),
            new Claim("actor_type", "Agent"));

        IGovernanceActorContext actor = CreateResolver(
            httpContext,
            options => options.AllowedActorTypesFromClaims = [GovernanceActorType.Agent]).ResolveActorContext();

        Assert.Equal("agent-007", actor.ActorId);
        Assert.Equal(GovernanceActorType.Agent, actor.ActorType);
    }

    [Theory]
    [InlineData("not-a-valid-type")]
    [InlineData("999")]
    public void ResolveActorContextFallsBackForUnrecognizedOrUndefinedClaim(string claimedActorType)
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            new Claim(ClaimTypes.NameIdentifier, "caller-1"),
            new Claim("actor_type", claimedActorType));

        IGovernanceActorContext actor = CreateResolver(
            httpContext,
            options => options.DefaultAuthenticatedActorType = GovernanceActorType.Agent).ResolveActorContext();

        Assert.Equal(GovernanceActorType.Agent, actor.ActorType);
        Assert.Equal("caller-1", actor.ActorId);
    }

    [Fact]
    public void ResolveActorContextDisablesClaimMappingWhenAllowedListIsEmpty()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            new Claim(ClaimTypes.NameIdentifier, "caller-1"),
            new Claim("actor_type", "Human"));

        IGovernanceActorContext actor = CreateResolver(
            httpContext,
            options =>
            {
                options.AllowedActorTypesFromClaims = [];
                options.DefaultAuthenticatedActorType = GovernanceActorType.Service;
            }).ResolveActorContext();

        Assert.Equal(GovernanceActorType.Service, actor.ActorType);
    }

    [Fact]
    public void ResolveActorContextRepresentsUnauthenticatedRequest()
    {
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };

        IGovernanceActorContext actor = CreateResolver(httpContext).ResolveActorContext();

        Assert.Equal(GovernanceActorType.Unknown, actor.ActorType);
        Assert.False(actor.IsAuthenticated);
    }

    [Fact]
    public void ResolveActorContextRepresentsMissingActorIdentifierAsUnknown()
    {
        DefaultHttpContext httpContext = CreateHttpContext(new Claim(ClaimTypes.Name, "No Identifier"));

        IGovernanceActorContext actor = CreateResolver(httpContext).ResolveActorContext();

        Assert.Equal(GovernanceActorType.Unknown, actor.ActorType);
        Assert.False(actor.IsAuthenticated);
    }

    [Fact]
    public void ActorOptionsRejectNullAllowedActorTypes()
    {
        HttpGovernanceActorContextOptions options = new()
        {
            AllowedActorTypesFromClaims = null!,
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(HttpGovernanceActorContextOptions.AllowedActorTypesFromClaims), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorOptionsRejectUndefinedAllowedActorType()
    {
        HttpGovernanceActorContextOptions options = new()
        {
            AllowedActorTypesFromClaims = [(GovernanceActorType)999],
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(HttpGovernanceActorContextOptions.AllowedActorTypesFromClaims), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorOptionsRejectUndefinedDefaultActorType()
    {
        HttpGovernanceActorContextOptions options = new()
        {
            DefaultAuthenticatedActorType = (GovernanceActorType)999,
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(HttpGovernanceActorContextOptions.DefaultAuthenticatedActorType), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ActorOptionsRejectBlankActorTypeClaimType(string? claimType)
    {
        HttpGovernanceActorContextOptions options = new()
        {
            ActorTypeClaimType = claimType!,
        };

        _ = Assert.Throws<InvalidOperationException>(options.Validate);
    }

    private static DefaultHttpContext CreateHttpContext(params Claim[] claims)
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
        };
    }

    private static HttpContextGovernanceActorContextResolver CreateResolver(
        HttpContext httpContext,
        Action<HttpGovernanceActorContextOptions>? configure = null)
    {
        HttpGovernanceActorContextOptions options = new();
        configure?.Invoke(options);

        return new HttpContextGovernanceActorContextResolver(
            new HttpContextAccessor { HttpContext = httpContext },
            Options.Create(options));
    }
}

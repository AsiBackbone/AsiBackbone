using System.Security.Claims;
using AsiBackbone.Core.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AsiBackbone.AspNetCore.Actors;

/// <summary>
/// Maps the current ASP.NET Core HTTP context into a framework-neutral AsiBackbone actor context.
/// </summary>
public sealed class HttpContextGovernanceActorContextResolver : IHttpGovernanceActorContextResolver
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly HttpGovernanceActorContextOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpContextGovernanceActorContextResolver" /> class.
    /// </summary>
    /// <param name="httpContextAccessor">The ASP.NET Core HTTP context accessor.</param>
    /// <param name="options">The actor mapping options.</param>
    public HttpContextGovernanceActorContextResolver(
        IHttpContextAccessor httpContextAccessor,
        IOptions<HttpGovernanceActorContextOptions> options)
    {
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
    }

    /// <inheritdoc />
    public IGovernanceActorContext ResolveActorContext()
    {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return ResolveUnauthenticatedActor;
        }

        string? actorId = FindFirstNonEmptyClaimValue(user, options.ActorIdClaimTypes);
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return ResolveUnauthenticatedActor;
        }

        string? displayName = FindFirstNonEmptyClaimValue(user, options.DisplayNameClaimTypes);
        GovernanceActorType actorType = ResolveActorType(user);

        return actorType switch
        {
            GovernanceActorType.Service => GovernanceActorContext.Service(actorId, displayName),
            GovernanceActorType.System => GovernanceActorContext.System,
            GovernanceActorType.Agent => GovernanceActorContext.Agent(actorId, displayName),
            GovernanceActorType.Human => GovernanceActorContext.Human(actorId, displayName),
            GovernanceActorType.Unknown => GovernanceActorContext.Unknown,
            _ => GovernanceActorContext.Human(actorId, displayName),
        };
    }

    private IGovernanceActorContext ResolveUnauthenticatedActor => string.IsNullOrWhiteSpace(options.UnauthenticatedDisplayName)
        ? GovernanceActorContext.Unknown
        : GovernanceActorContext.Human(
            GovernanceActorContext.UnknownActorId,
            options.UnauthenticatedDisplayName,
            isAuthenticated: false);

    private GovernanceActorType ResolveActorType(ClaimsPrincipal user)
    {
        string? actorTypeValue = FindFirstNonEmptyClaimValue(user, [options.ActorTypeClaimType]);

        return Enum.TryParse(actorTypeValue, ignoreCase: true, out GovernanceActorType actorType)
            && Enum.IsDefined(actorType)
            && options.AllowedActorTypesFromClaims.Contains(actorType)
            ? actorType
            : options.DefaultAuthenticatedActorType;
    }

    private static string? FindFirstNonEmptyClaimValue(ClaimsPrincipal user, IEnumerable<string> claimTypes)
    {
        foreach (string claimType in claimTypes.Where(static claimType => !string.IsNullOrWhiteSpace(claimType)))
        {
            Claim? claim = user.FindFirst(claimType);
            if (!string.IsNullOrWhiteSpace(claim?.Value))
            {
                return claim.Value.Trim();
            }
        }

        return null;
    }
}

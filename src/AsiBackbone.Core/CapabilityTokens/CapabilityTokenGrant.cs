using System.Collections.ObjectModel;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.Core.CapabilityTokens;

/// <summary>
/// Represents a provider-neutral, short-lived capability grant for follow-on governed execution.
/// </summary>
/// <remarks>
/// The grant is a metadata model, not a bearer-token format. Hosts decide how this grant is serialized,
/// transported, protected, and bound to their authentication and authorization systems.
/// </remarks>
public sealed class CapabilityTokenGrant
{
    private static readonly ReadOnlyCollection<string> EmptyScopes =
        Array.AsReadOnly(Array.Empty<string>());

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private CapabilityTokenGrant(
        string tokenId,
        string issuer,
        string audience,
        IReadOnlyList<string> scopes,
        DateTimeOffset issuedUtc,
        DateTimeOffset? notBeforeUtc,
        DateTimeOffset expiresUtc,
        string? subjectId,
        string? operationName,
        string? policyVersion,
        string? policyHash,
        string? acknowledgmentId,
        string? handshakeId,
        string? gatewayBinding,
        string? resourceBinding,
        IReadOnlyDictionary<string, string> metadata,
        int? maxUseCount,
        string? schemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentNullException.ThrowIfNull(scopes);

        if (scopes.Count == 0)
        {
            throw new ArgumentException("At least one capability scope is required.", nameof(scopes));
        }

        DateTimeOffset normalizedIssuedUtc = issuedUtc.ToUniversalTime();
        DateTimeOffset? normalizedNotBeforeUtc = notBeforeUtc?.ToUniversalTime();
        DateTimeOffset normalizedExpiresUtc = expiresUtc.ToUniversalTime();

        if (normalizedNotBeforeUtc.HasValue && normalizedNotBeforeUtc.Value > normalizedExpiresUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(notBeforeUtc), notBeforeUtc, "Not-before time must be earlier than or equal to expiration time.");
        }

        if (normalizedIssuedUtc > normalizedExpiresUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresUtc), expiresUtc, "Expiration time must be later than or equal to issued time.");
        }

        TokenId = tokenId.Trim();
        Issuer = issuer.Trim();
        Audience = audience.Trim();
        Scopes = scopes;
        IssuedUtc = normalizedIssuedUtc;
        NotBeforeUtc = normalizedNotBeforeUtc;
        ExpiresUtc = normalizedExpiresUtc;
        SubjectId = NormalizeOptional(subjectId);
        OperationName = NormalizeOptional(operationName);
        PolicyVersion = NormalizeOptional(policyVersion);
        PolicyHash = NormalizeOptional(policyHash);
        AcknowledgmentId = NormalizeOptional(acknowledgmentId);
        HandshakeId = NormalizeOptional(handshakeId);
        GatewayBinding = NormalizeOptional(gatewayBinding);
        ResourceBinding = NormalizeOptional(resourceBinding);
        Metadata = metadata;
        MaxUseCount = maxUseCount;
        SchemaVersion = AsiBackboneSchemaVersions.Normalize(schemaVersion);
    }

    /// <summary>
    /// Gets the maximum number of times the issuer permits this grant to be used, when the issuer bound one.
    /// </summary>
    /// <remarks>
    /// A use limit supplied only at the validation call site is a local policy choice that the issuer never authorized and
    /// that nothing in the signature covers. When this value is present it is part of the signed payload, so a relying
    /// party cannot widen it. Validation uses the narrower of this value and any limit the caller supplies.
    /// </remarks>
    public int? MaxUseCount { get; }

    /// <summary>
    /// Gets the stable grant identifier used for validation and replay checks.
    /// </summary>
    public string TokenId { get; }

    /// <summary>
    /// Gets the issuer that created the grant.
    /// </summary>
    public string Issuer { get; }

    /// <summary>
    /// Gets the intended audience for the grant.
    /// </summary>
    public string Audience { get; }

    /// <summary>
    /// Gets the least-privilege scopes carried by the grant.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; }

    /// <summary>
    /// Gets the UTC timestamp when the grant was issued.
    /// </summary>
    public DateTimeOffset IssuedUtc { get; }

    /// <summary>
    /// Gets the UTC timestamp before which the grant is not valid.
    /// </summary>
    public DateTimeOffset? NotBeforeUtc { get; }

    /// <summary>
    /// Gets the UTC timestamp when the grant expires.
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; }

    /// <summary>
    /// Gets the host-defined subject identifier, when supplied.
    /// </summary>
    public string? SubjectId { get; }

    /// <summary>
    /// Gets the operation name or action family the grant is intended to authorize.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    /// Gets the policy version bound to the grant, when supplied.
    /// </summary>
    public string? PolicyVersion { get; }

    /// <summary>
    /// Gets the policy hash bound to the grant, when supplied.
    /// </summary>
    public string? PolicyHash { get; }

    /// <summary>
    /// Gets the acknowledgment identifier bound to the grant, when supplied.
    /// </summary>
    public string? AcknowledgmentId { get; }

    /// <summary>
    /// Gets the handshake identifier bound to the grant, when supplied.
    /// </summary>
    public string? HandshakeId { get; }

    /// <summary>
    /// Gets the optional gateway binding used to limit execution context.
    /// </summary>
    public string? GatewayBinding { get; }

    /// <summary>
    /// Gets the optional resource binding used to limit the target resource.
    /// </summary>
    public string? ResourceBinding { get; }

    /// <summary>
    /// Gets the canonical schema version for this grant.
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    /// Gets provider-neutral metadata carried with the grant.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether an acknowledgment reference is present.
    /// </summary>
    public bool HasAcknowledgmentReference => AcknowledgmentId is not null;

    /// <summary>
    /// Gets a value indicating whether a handshake reference is present.
    /// </summary>
    public bool HasHandshakeReference => HandshakeId is not null;

    /// <summary>
    /// Gets a value indicating whether additional metadata is present.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Creates a provider-neutral capability grant.
    /// </summary>
    public static CapabilityTokenGrant Create(
        string tokenId,
        string issuer,
        string audience,
        IEnumerable<string> scopes,
        DateTimeOffset issuedUtc,
        DateTimeOffset expiresUtc,
        DateTimeOffset? notBeforeUtc = null,
        string? subjectId = null,
        string? operationName = null,
        string? policyVersion = null,
        string? policyHash = null,
        string? acknowledgmentId = null,
        string? handshakeId = null,
        string? gatewayBinding = null,
        string? resourceBinding = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        int? maxUseCount = null,
        string? schemaVersion = null)
    {
        return maxUseCount is < 1
            ? throw new ArgumentOutOfRangeException(nameof(maxUseCount), maxUseCount, "A bound use limit must be greater than zero.")
            : new CapabilityTokenGrant(
            tokenId,
            issuer,
            audience,
            NormalizeScopes(scopes),
            issuedUtc,
            notBeforeUtc,
            expiresUtc,
            subjectId,
            operationName,
            policyVersion,
            policyHash,
            acknowledgmentId,
            handshakeId,
            gatewayBinding,
            resourceBinding,
            NormalizeMetadata(metadata),
            maxUseCount,
            schemaVersion);
    }

    private static ReadOnlyCollection<string> NormalizeScopes(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        string[] normalizedScopes = [.. scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)];

        return normalizedScopes.Length == 0
            ? EmptyScopes
            : Array.AsReadOnly(normalizedScopes);
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> normalizedMetadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            normalizedMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}

using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Http;

namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Represents normalized AsiBackbone governance metadata resolved from an ASP.NET Core endpoint.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceDescriptor
{
    private static readonly ReadOnlyCollection<Type> EmptyPolicyTypes = Array.AsReadOnly(Array.Empty<Type>());
    private static readonly ReadOnlyCollection<string> EmptyScopes = Array.AsReadOnly(Array.Empty<string>());

    private readonly IReadOnlyDictionary<string, string> fullMetadata;
    private readonly IReadOnlyDictionary<string, string> reducedMetadata;

    private AsiBackboneEndpointGovernanceDescriptor(
        string operationName,
        IReadOnlyList<Type> policyTypes,
        bool? shortCircuitOnFirstDenial,
        bool requiresLiabilityHandshake,
        IReadOnlyList<string> capabilityScopes,
        bool emitGovernanceAudit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        OperationName = operationName.Trim();
        PolicyTypes = policyTypes;
        ShortCircuitOnFirstDenial = shortCircuitOnFirstDenial;
        RequiresLiabilityHandshake = requiresLiabilityHandshake;
        CapabilityScopes = capabilityScopes;
        EmitGovernanceAudit = emitGovernanceAudit;
        reducedMetadata = CreateReducedMetadata(OperationName, PolicyTypes);
        fullMetadata = CreateFullMetadata();
    }

    /// <summary>
    /// Gets the operation name used for audit residue and acknowledgment challenge construction.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the policy marker or resolver types attached to the endpoint.
    /// </summary>
    public IReadOnlyList<Type> PolicyTypes { get; }

    /// <summary>
    /// Gets an endpoint-scoped first-denial short-circuit preference, when endpoint metadata supplied one.
    /// </summary>
    public bool? ShortCircuitOnFirstDenial { get; }

    /// <summary>
    /// Gets a value indicating whether liability-handshake support is requested.
    /// </summary>
    public bool RequiresLiabilityHandshake { get; }

    /// <summary>
    /// Gets the required capability-grant scopes attached to the endpoint.
    /// </summary>
    public IReadOnlyList<string> CapabilityScopes { get; }

    /// <summary>
    /// Gets a value indicating whether governance audit emission is requested.
    /// </summary>
    public bool EmitGovernanceAudit { get; }

    /// <summary>
    /// Gets a value indicating whether the endpoint contains any AsiBackbone governance metadata.
    /// </summary>
    public bool HasGovernanceMetadata => PolicyTypes.Count > 0
        || ShortCircuitOnFirstDenial.HasValue
        || RequiresLiabilityHandshake
        || CapabilityScopes.Count > 0
        || EmitGovernanceAudit;

    /// <summary>
    /// Creates a descriptor from the selected ASP.NET Core endpoint.
    /// </summary>
    /// <param name="endpoint">The selected endpoint.</param>
    /// <returns>A normalized descriptor.</returns>
    public static AsiBackboneEndpointGovernanceDescriptor FromEndpoint(Endpoint? endpoint)
    {
        if (endpoint is null)
        {
            return None("unresolved-endpoint");
        }

        List<Type>? policyTypes = null;
        foreach (IAsiBackboneEndpointGovernancePolicyMetadata metadata in endpoint.Metadata.GetOrderedMetadata<IAsiBackboneEndpointGovernancePolicyMetadata>())
        {
            if (metadata.PolicyType is not Type policyType || ContainsPolicyType(policyTypes, policyType))
            {
                continue;
            }

            policyTypes ??= [];
            policyTypes.Add(policyType);
        }

        List<string>? capabilityScopes = null;
        foreach (IAsiBackboneEndpointCapabilityGrantMetadata metadata in endpoint.Metadata.GetOrderedMetadata<IAsiBackboneEndpointCapabilityGrantMetadata>())
        {
            if (string.IsNullOrWhiteSpace(metadata.Scope))
            {
                continue;
            }

            string scope = metadata.Scope.Trim();
            if (ContainsScope(capabilityScopes, scope))
            {
                continue;
            }

            capabilityScopes ??= [];
            capabilityScopes.Add(scope);
        }

        bool? shortCircuitOnFirstDenial = null;
        foreach (IAsiBackboneEndpointPolicyEvaluationOptionsMetadata metadata in endpoint.Metadata.GetOrderedMetadata<IAsiBackboneEndpointPolicyEvaluationOptionsMetadata>())
        {
            shortCircuitOnFirstDenial = metadata.ShortCircuitOnFirstDenial;
        }

        bool requiresLiabilityHandshake = false;
        foreach (IAsiBackboneEndpointLiabilityHandshakeMetadata metadata in endpoint.Metadata.GetOrderedMetadata<IAsiBackboneEndpointLiabilityHandshakeMetadata>())
        {
            if (metadata.RequiresLiabilityHandshake)
            {
                requiresLiabilityHandshake = true;
                break;
            }
        }

        bool emitGovernanceAudit = false;
        foreach (IAsiBackboneEndpointAuditEmissionMetadata metadata in endpoint.Metadata.GetOrderedMetadata<IAsiBackboneEndpointAuditEmissionMetadata>())
        {
            if (metadata.EmitGovernanceAudit)
            {
                emitGovernanceAudit = true;
                break;
            }
        }

        return new AsiBackboneEndpointGovernanceDescriptor(
            ResolveOperationName(endpoint),
            policyTypes is null ? EmptyPolicyTypes : Array.AsReadOnly(policyTypes.ToArray()),
            shortCircuitOnFirstDenial,
            requiresLiabilityHandshake,
            capabilityScopes is null ? EmptyScopes : Array.AsReadOnly(capabilityScopes.ToArray()),
            emitGovernanceAudit);
    }

    /// <summary>
    /// Creates a descriptor that does not request governance handling.
    /// </summary>
    /// <param name="operationName">The operation name to associate with the descriptor.</param>
    /// <returns>A descriptor with no governance metadata.</returns>
    public static AsiBackboneEndpointGovernanceDescriptor None(string operationName)
    {
        return new AsiBackboneEndpointGovernanceDescriptor(
            operationName,
            EmptyPolicyTypes,
            shortCircuitOnFirstDenial: null,
            requiresLiabilityHandshake: false,
            EmptyScopes,
            emitGovernanceAudit: false);
    }

    /// <summary>
    /// Converts descriptor values into safe metadata for framework-neutral governance evaluation and audit residue.
    /// </summary>
    /// <returns>A normalized metadata dictionary.</returns>
    public IReadOnlyDictionary<string, string> ToMetadata()
    {
        return ToMetadata(AsiBackboneEndpointGovernanceMetadataMode.Full);
    }

    /// <summary>
    /// Converts descriptor values into safe metadata for framework-neutral governance evaluation and audit residue.
    /// </summary>
    /// <param name="metadataMode">The endpoint metadata mode to apply.</param>
    /// <returns>A normalized metadata dictionary.</returns>
    public IReadOnlyDictionary<string, string> ToMetadata(AsiBackboneEndpointGovernanceMetadataMode metadataMode)
    {
        return metadataMode switch
        {
            AsiBackboneEndpointGovernanceMetadataMode.Full => fullMetadata,
            AsiBackboneEndpointGovernanceMetadataMode.Reduced => reducedMetadata,
            _ => throw new ArgumentOutOfRangeException(nameof(metadataMode), metadataMode, "Endpoint governance metadata mode is not supported.")
        };
    }

    private static bool ContainsPolicyType(List<Type>? policyTypes, Type policyType)
    {
        if (policyTypes is null)
        {
            return false;
        }

        foreach (Type currentPolicyType in policyTypes)
        {
            if (currentPolicyType == policyType)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsScope(List<string>? capabilityScopes, string scope)
    {
        if (capabilityScopes is null)
        {
            return false;
        }

        foreach (string currentScope in capabilityScopes)
        {
            if (string.Equals(currentScope, scope, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private ReadOnlyDictionary<string, string> CreateFullMetadata()
    {
        int metadataCapacity = 3
            + (PolicyTypes.Count > 0 ? 1 : 0)
            + (ShortCircuitOnFirstDenial.HasValue ? 1 : 0)
            + (CapabilityScopes.Count > 0 ? 1 : 0);

        var metadata = new Dictionary<string, string>(metadataCapacity, StringComparer.Ordinal)
        {
            ["endpoint.operation_name"] = OperationName,
            ["endpoint.requires_liability_handshake"] = RequiresLiabilityHandshake ? "true" : "false",
            ["endpoint.emit_governance_audit"] = EmitGovernanceAudit ? "true" : "false"
        };

        if (PolicyTypes.Count > 0)
        {
            metadata["endpoint.policy_types"] = JoinPolicyTypeNames(PolicyTypes);
        }

        if (ShortCircuitOnFirstDenial.HasValue)
        {
            metadata["endpoint.short_circuit_on_first_denial"] = ShortCircuitOnFirstDenial.Value ? "true" : "false";
        }

        if (CapabilityScopes.Count > 0)
        {
            metadata["endpoint.capability_scopes"] = string.Join(",", CapabilityScopes);
        }

        return new ReadOnlyDictionary<string, string>(metadata);
    }

    private static ReadOnlyDictionary<string, string> CreateReducedMetadata(
        string operationName,
        IReadOnlyList<Type> policyTypes)
    {
        // Policy types are retained in Reduced mode. A host decision policy that varies its outcome by
        // endpoint.policy_types would otherwise stop seeing the marker under Reduced and silently fall back to
        // uniform behavior, which is a permissive change made by a metadata setting rather than a policy one.
        var metadata = new Dictionary<string, string>(policyTypes.Count > 0 ? 2 : 1, StringComparer.Ordinal)
        {
            ["endpoint.operation_name"] = operationName
        };

        if (policyTypes.Count > 0)
        {
            metadata["endpoint.policy_types"] = JoinPolicyTypeNames(policyTypes);
        }

        return new ReadOnlyDictionary<string, string>(metadata);
    }

    private static string JoinPolicyTypeNames(IReadOnlyList<Type> policyTypes)
    {
        string[] policyTypeNames = new string[policyTypes.Count];

        for (int index = 0; index < policyTypeNames.Length; index++)
        {
            Type policyType = policyTypes[index];
            policyTypeNames[index] = policyType.FullName ?? policyType.Name;
        }

        return string.Join(",", policyTypeNames);
    }

    private static string ResolveOperationName(Endpoint endpoint)
    {
        return string.IsNullOrWhiteSpace(endpoint.DisplayName)
            ? "aspnetcore.endpoint"
            : endpoint.DisplayName.Trim();
    }
}

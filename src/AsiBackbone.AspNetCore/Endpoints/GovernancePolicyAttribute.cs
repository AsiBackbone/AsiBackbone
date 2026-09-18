namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Marks an ASP.NET Core endpoint with the host-defined AsiBackbone policy type that governs it.
/// </summary>
/// <remarks>
/// <para>
/// The recorded type is a marker, not an enforcement rule. The framework does not resolve it or select constraints
/// from it: the registered policy evaluator evaluates every registered constraint on every governed endpoint,
/// whichever policy type an endpoint carries. Presence of this attribute is what causes policy evaluation to run at
/// all; the type identifies which policy the host intends, and reaches evaluation as the
/// <c>endpoint.policy_types</c> metadata entry where a host-supplied decision policy can read it. Marking two
/// endpoints with different policy types does not by itself make them evaluate differently.
/// </para>
/// <para>
/// Named <c>RequireGovernancePolicyAttribute</c> before 6.0. The route-builder extension of the same name became
/// <c>MarkGovernancePolicy</c> because "Require" overstated what the marker does; the attribute is renamed for the same
/// reason, so the attribute and route-builder paths use consistent vocabulary.
/// </para>
/// <para>
/// Initializes a new instance of the <see cref="GovernancePolicyAttribute" /> class.
/// </para>
/// </remarks>
/// <param name="policyType">The host-defined policy marker or decision policy type associated with the endpoint.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class GovernancePolicyAttribute(Type policyType) : Attribute, IEndpointGovernancePolicyMetadata
{

    /// <inheritdoc />
    public Type PolicyType { get; } = policyType ?? throw new ArgumentNullException(nameof(policyType));
}

/// <summary>
/// Marks an ASP.NET Core endpoint as requesting latency-optimized fast-abort policy evaluation after the first denied constraint result.
/// </summary>
/// <remarks>
/// This metadata is resolved into the endpoint governance descriptor and exported into framework-neutral evaluation metadata.
/// Hosts still own how endpoint metadata is mapped into evaluator configuration.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="ShortCircuitOnFirstDenialAttribute" /> class.
/// </remarks>
/// <param name="enabled">Whether first-denial short-circuit metadata is enabled for the endpoint.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ShortCircuitOnFirstDenialAttribute(bool enabled = true) : Attribute, IEndpointPolicyEvaluationOptionsMetadata
{

    /// <inheritdoc />
    public bool? ShortCircuitOnFirstDenial { get; } = enabled;
}

/// <summary>
/// Marks an ASP.NET Core endpoint as requiring liability-handshake support when a governance decision requires acknowledgment.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireLiabilityHandshakeAttribute : Attribute, IEndpointLiabilityHandshakeMetadata
{
    /// <inheritdoc />
    public bool RequiresLiabilityHandshake => true;
}

/// <summary>
/// Marks an ASP.NET Core endpoint as requiring a host-validated capability grant before execution.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireCapabilityGrantAttribute : Attribute, IEndpointCapabilityGrantMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequireCapabilityGrantAttribute" /> class.
    /// </summary>
    /// <param name="scope">The required capability-grant scope.</param>
    public RequireCapabilityGrantAttribute(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        Scope = scope.Trim();
    }

    /// <inheritdoc />
    public string Scope { get; }
}

/// <summary>
/// Marks an ASP.NET Core endpoint as requesting governance audit emission through the host-owned audit path.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class EmitGovernanceAuditAttribute : Attribute, IEndpointAuditEmissionMetadata
{
    /// <inheritdoc />
    public bool EmitGovernanceAudit => true;
}

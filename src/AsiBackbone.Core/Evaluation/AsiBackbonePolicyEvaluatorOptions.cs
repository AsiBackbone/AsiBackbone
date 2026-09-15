namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Provides host-configurable options for the default AsiBackbone policy evaluator.
/// </summary>
/// <remarks>
/// Options remain mutable while a host is configuring them. The default evaluator validates and freezes the supplied
/// instance during construction so later caller mutation cannot change evaluator behavior. Configure a separate options
/// instance for each evaluator posture that must differ.
/// </remarks>
public class AsiBackbonePolicyEvaluatorOptions
{
    private const string DefaultNoConstraintsReasonMessage =
        "No policy constraints were registered or supplied for evaluation.";

    private const string DefaultConstraintExceptionReasonMessage =
        "A policy constraint failed during evaluation. The operation was denied by the evaluator failure policy.";

    private const string DefaultThreatContributorExceptionReasonMessage =
        "A threat model contributor failed during evaluation. The operation was denied by the evaluator failure policy.";
    private bool isFrozen;

    /// <summary>
    /// Gets the default machine-readable reason code used when strict empty-policy evaluation denies execution.
    /// </summary>
    public const string DefaultNoConstraintsReasonCode = "asibackbone.policy.no_constraints";

    /// <summary>
    /// Gets the default machine-readable reason code used when constraint exception evaluation denies execution.
    /// </summary>
    public const string DefaultConstraintExceptionReasonCode = "asibackbone.policy.constraint_exception";

    /// <summary>
    /// Gets the default machine-readable reason code used when threat contributor exception evaluation denies execution.
    /// </summary>
    public const string DefaultThreatContributorExceptionReasonCode = "asibackbone.threat.contributor_exception";

    /// <summary>
    /// Gets or sets a value indicating whether evaluation should deny when no constraints are registered or supplied.
    /// </summary>
    public bool DenyWhenNoConstraints
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = true;

    /// <summary>
    /// Gets or sets a value indicating whether evaluation should stop after the first denied constraint result.
    /// </summary>
    public bool ShortCircuitOnFirstDenial
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether constraint exceptions should be converted into denied governance decisions.
    /// </summary>
    public bool TreatConstraintExceptionAsDenial
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = true;

    /// <summary>
    /// Gets or sets a value indicating whether threat contributor exceptions should be converted into denied governance decisions.
    /// </summary>
    public bool TreatThreatContributorExceptionAsDenial
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = true;

    /// <summary>
    /// Gets or sets a value indicating whether threat assessment outcomes should be protected from being downgraded to pure allow decisions.
    /// </summary>
    public bool PreventThreatAssessmentAllowDowngrade
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = true;

    /// <summary>
    /// Gets or sets the machine-readable reason code used when <see cref="DenyWhenNoConstraints" /> denies an empty policy.
    /// </summary>
    public string NoConstraintsReasonCode
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = DefaultNoConstraintsReasonCode;

    /// <summary>
    /// Gets or sets the reason message used when <see cref="DenyWhenNoConstraints" /> denies an empty policy.
    /// </summary>
    public string NoConstraintsReasonMessage
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = DefaultNoConstraintsReasonMessage;

    /// <summary>
    /// Gets or sets the machine-readable reason code used when <see cref="TreatConstraintExceptionAsDenial" /> denies a constraint exception.
    /// </summary>
    public string ConstraintExceptionReasonCode
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = DefaultConstraintExceptionReasonCode;

    /// <summary>
    /// Gets or sets the reason message used when <see cref="TreatConstraintExceptionAsDenial" /> denies a constraint exception.
    /// </summary>
    public string ConstraintExceptionReasonMessage
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = DefaultConstraintExceptionReasonMessage;

    /// <summary>
    /// Gets or sets the machine-readable reason code used when <see cref="TreatThreatContributorExceptionAsDenial" /> denies a contributor exception.
    /// </summary>
    public string ThreatContributorExceptionReasonCode
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = DefaultThreatContributorExceptionReasonCode;

    /// <summary>
    /// Gets or sets the reason message used when <see cref="TreatThreatContributorExceptionAsDenial" /> denies a contributor exception.
    /// </summary>
    public string ThreatContributorExceptionReasonMessage
    {
        get;
        set
        {
            ThrowIfFrozen();
            field = value;
        }
    } = DefaultThreatContributorExceptionReasonMessage;

    /// <summary>
    /// Validates evaluator options and freezes the instance for evaluator use.
    /// </summary>
    /// <remarks>
    /// The method is idempotent. After successful validation, attempts to change any option throw an
    /// <see cref="InvalidOperationException" /> so a constructed evaluator cannot observe configuration drift.
    /// </remarks>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(NoConstraintsReasonCode))
        {
            throw new InvalidOperationException($"{nameof(NoConstraintsReasonCode)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(NoConstraintsReasonMessage))
        {
            throw new InvalidOperationException($"{nameof(NoConstraintsReasonMessage)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(ConstraintExceptionReasonCode))
        {
            throw new InvalidOperationException($"{nameof(ConstraintExceptionReasonCode)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(ConstraintExceptionReasonMessage))
        {
            throw new InvalidOperationException($"{nameof(ConstraintExceptionReasonMessage)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(ThreatContributorExceptionReasonCode))
        {
            throw new InvalidOperationException($"{nameof(ThreatContributorExceptionReasonCode)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(ThreatContributorExceptionReasonMessage))
        {
            throw new InvalidOperationException($"{nameof(ThreatContributorExceptionReasonMessage)} must not be empty.");
        }

        isFrozen = true;
    }

    private void ThrowIfFrozen()
    {
        if (isFrozen)
        {
            throw new InvalidOperationException(
                "Evaluator options cannot be changed after they have been validated for evaluator construction.");
        }
    }
}

namespace AsiBackbone.AspNetCore.Outbox;

/// <summary>
/// Provides host-owned scheduling options for the governance outbox drain worker.
/// </summary>
/// <remarks>
/// The hosted worker is local to the process in which it is registered. In horizontally scaled deployments, each
/// replica may run its own worker unless the host disables extra replicas, partitions work, or adds durable claiming.
/// </remarks>
public sealed class AsiBackboneGovernanceOutboxDrainWorkerOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the hosted drain worker should run.
    /// </summary>
    /// <remarks>
    /// Runtime changes supplied through <c>IOptionsMonitor</c> pause or resume new drain cycles without restarting the
    /// process. A worker that starts disabled validates its scoped drain dependencies once, then remains alive and waits
    /// without starting drain cycles or retaining scoped services.
    /// Re-enabling normally takes effect immediately through the options change notification, with the configured
    /// <see cref="PollingInterval" /> serving as a fallback observation interval.
    /// For multi-replica hosts, enable this only on the selected worker role or partition owner unless the outbox store
    /// provides host-owned claim/lease behavior before provider emission.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of pending or retry-ready entries attempted per drain pass.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the interval between drain passes when the worker is enabled.
    /// </summary>
    /// <remarks>
    /// While the worker is disabled, this interval is also the fallback check period when the configured options source
    /// does not raise a change notification.
    /// </remarks>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the delay after an unexpected worker-level failure before the next drain pass is attempted.
    /// </summary>
    public TimeSpan FailureDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the retry clock used when finding retry-ready entries.
    /// </summary>
    public Func<DateTimeOffset> RetryClock { get; set; } = static () => DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether a final drain pass should be attempted during host shutdown.
    /// </summary>
    /// <remarks>
    /// Avoid enabling shutdown drains on many replicas against the same durable outbox unless duplicate-emission behavior
    /// is controlled through partitioning, durable claiming, or provider-side idempotency.
    /// </remarks>
    public bool DrainOnShutdown { get; set; }

    /// <summary>
    /// Gets or sets the maximum amount of time allowed for an optional shutdown drain pass.
    /// </summary>
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Validates the configured worker options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required worker option is invalid.</exception>
    public void Validate()
    {
        if (BatchSize <= 0)
        {
            throw new InvalidOperationException("Governance outbox drain batch size must be greater than zero.");
        }

        if (PollingInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Governance outbox drain polling interval must be greater than zero.");
        }

        if (FailureDelay <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Governance outbox drain failure delay must be greater than zero.");
        }

        if (ShutdownDrainTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Governance outbox drain shutdown timeout must be greater than zero.");
        }

        if (RetryClock is null)
        {
            throw new InvalidOperationException("Governance outbox drain retry clock must be configured.");
        }
    }
}

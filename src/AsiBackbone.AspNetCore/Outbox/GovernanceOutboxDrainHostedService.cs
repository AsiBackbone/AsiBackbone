using AsiBackbone.Core.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsiBackbone.AspNetCore.Outbox;

/// <summary>
/// Runs the provider-neutral outbox drain from an ASP.NET Core or generic-host background worker.
/// </summary>
/// <remarks>
/// Hosting remains outside Core. Startup validates the scoped drain dependency graph and fails when the store or emitter
/// cannot be resolved. Each drain cycle then resolves the drain through a new scoped service provider so durable providers
/// that depend on scoped infrastructure, such as a host-owned EF Core <c>DbContext</c>, remain safe to use. Runtime changes
/// to <see cref="GovernanceOutboxDrainWorkerOptions.Enabled" /> pause or resume new drain cycles without
/// terminating the hosted service.
/// </remarks>
/// <param name="scopeFactory">The factory used to create a scope for each drain cycle.</param>
/// <param name="optionsMonitor">The monitored worker options.</param>
/// <param name="logger">The worker logger.</param>
/// <param name="timeProvider">The clock used for drain timestamps unless <see cref="GovernanceOutboxDrainWorkerOptions.RetryClock" /> has been assigned a custom delegate. Defaults to <see cref="TimeProvider.System" />; dependency injection supplies a registered <see cref="TimeProvider" />.</param>
public sealed class GovernanceOutboxDrainHostedService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<GovernanceOutboxDrainWorkerOptions> optionsMonitor,
    ILogger<GovernanceOutboxDrainHostedService> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogShutdownDrainCanceled = LoggerMessage.Define(
        LogLevel.Debug,
        new EventId(19801, nameof(LogShutdownDrainCanceled)),
        "Governance outbox shutdown drain was canceled.");

    private static readonly Action<ILogger, Exception?> LogShutdownDrainFailed = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(19802, nameof(LogShutdownDrainFailed)),
        "Governance outbox shutdown drain failed.");

    private static readonly Action<ILogger, Exception?> LogWorkerDisabled = LoggerMessage.Define(
        LogLevel.Debug,
        new EventId(19803, nameof(LogWorkerDisabled)),
        "Governance outbox drain worker is disabled.");

    private static readonly Action<ILogger, int, Exception?> LogDrainAttempted = LoggerMessage.Define<int>(
        LogLevel.Debug,
        new EventId(19804, nameof(LogDrainAttempted)),
        "Governance outbox drain attempted {DrainedCount} entries.");

    private static readonly Action<ILogger, Exception?> LogWorkerFailed = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(19805, nameof(LogWorkerFailed)),
        "Governance outbox drain worker failed before the next polling interval.");

    private static readonly Action<ILogger, Exception?> LogStartupValidationFailed = LoggerMessage.Define(
        LogLevel.Critical,
        new EventId(19806, nameof(LogStartupValidationFailed)),
        "Governance outbox drain worker startup validation failed. Ensure an outbox store and governance emitter are registered.");

    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly IOptionsMonitor<GovernanceOutboxDrainWorkerOptions> optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
    private readonly ILogger<GovernanceOutboxDrainHostedService> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Lock optionsChangedSync = new();
    private TaskCompletionSource optionsChanged = CreateOptionsChangedSource();
    private long optionsVersion;

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            optionsMonitor.CurrentValue.Validate();

            using IServiceScope scope = scopeFactory.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<GovernanceOutboxDrain>();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogStartupValidationFailed(logger, exception);
            throw;
        }

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        GovernanceOutboxDrainWorkerOptions options = optionsMonitor.CurrentValue;

        if (options.Enabled && options.DrainOnShutdown && !cancellationToken.IsCancellationRequested)
        {
            using var shutdownDrainCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            shutdownDrainCancellation.CancelAfter(options.ShutdownDrainTimeout);

            try
            {
                _ = await DrainOnceAsync(options, shutdownDrainCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (shutdownDrainCancellation.IsCancellationRequested)
            {
                LogShutdownDrainCanceled(logger, null);
            }
            catch (Exception ex)
            {
                LogShutdownDrainFailed(logger, ex);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IDisposable? optionsChangeRegistration = optionsMonitor.OnChange((_, _) => SignalOptionsChanged());
        bool disabledLogged = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            (long observedVersion, Task optionsChangedTask) = CaptureOptionsChangeState();
            GovernanceOutboxDrainWorkerOptions options = optionsMonitor.CurrentValue;

            if (!options.Enabled)
            {
                if (!disabledLogged)
                {
                    LogWorkerDisabled(logger, null);
                    disabledLogged = true;
                }

                await WaitForDelayOrOptionsChangeAsync(
                    options.PollingInterval,
                    observedVersion,
                    optionsChangedTask,
                    stoppingToken).ConfigureAwait(false);
                continue;
            }

            disabledLogged = false;

            try
            {
                int drainedCount = await DrainOnceAsync(options, stoppingToken).ConfigureAwait(false);
                LogDrainAttempted(logger, drainedCount, null);
                await WaitForDelayOrOptionsChangeAsync(
                    options.PollingInterval,
                    observedVersion,
                    optionsChangedTask,
                    stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWorkerFailed(logger, ex);
                await WaitForDelayOrOptionsChangeAsync(
                    options.FailureDelay,
                    observedVersion,
                    optionsChangedTask,
                    stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<int> DrainOnceAsync(
        GovernanceOutboxDrainWorkerOptions options,
        CancellationToken cancellationToken)
    {
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        using IServiceScope scope = scopeFactory.CreateScope();
        GovernanceOutboxDrain drain = scope.ServiceProvider.GetRequiredService<GovernanceOutboxDrain>();
        DateTimeOffset retryUtc = ResolveDrainUtc(options);
        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            retryUtc,
            options.BatchSize,
            cancellationToken)
            .ConfigureAwait(false);

        return drainedEntries.Count;
    }

    private async ValueTask WaitForDelayOrOptionsChangeAsync(
        TimeSpan delay,
        long observedVersion,
        Task optionsChangedTask,
        CancellationToken cancellationToken)
    {
        lock (optionsChangedSync)
        {
            if (optionsVersion != observedVersion)
            {
                return;
            }
        }

        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(delay, delayCancellation.Token);
        Task completedTask = await Task.WhenAny(delayTask, optionsChangedTask).ConfigureAwait(false);

        if (completedTask == optionsChangedTask)
        {
            await delayCancellation.CancelAsync().ConfigureAwait(false);
            return;
        }

        await delayTask.ConfigureAwait(false);
    }

    private (long Version, Task ChangedTask) CaptureOptionsChangeState()
    {
        lock (optionsChangedSync)
        {
            return (optionsVersion, optionsChanged.Task);
        }
    }

    private void SignalOptionsChanged()
    {
        TaskCompletionSource completedSource;
        lock (optionsChangedSync)
        {
            completedSource = optionsChanged;
            optionsChanged = CreateOptionsChangedSource();
            optionsVersion++;
        }

        _ = completedSource.TrySetResult();
    }

    /// <summary>
    /// Resolves the drain timestamp for one drain cycle.
    /// </summary>
    /// <remarks>
    /// A custom <see cref="GovernanceOutboxDrainWorkerOptions.RetryClock" /> is honored for compatibility. While it keeps
    /// its default value, the registered <see cref="TimeProvider" /> supplies the time, so the worker, the drain, and the
    /// signing providers read one clock.
    /// </remarks>
    private DateTimeOffset ResolveDrainUtc(GovernanceOutboxDrainWorkerOptions options)
    {
        DateTimeOffset utcNow = ReferenceEquals(options.RetryClock, GovernanceOutboxDrainWorkerOptions.DefaultRetryClock)
            ? timeProvider.GetUtcNow()
            : options.RetryClock();

        return utcNow.ToUniversalTime();
    }

    private static TaskCompletionSource CreateOptionsChangedSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

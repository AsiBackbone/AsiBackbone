using AsiBackbone.Core.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsiBackbone.AspNetCore.Outbox;

/// <summary>
/// Runs the provider-neutral governance outbox drain from an ASP.NET Core or generic-host background worker.
/// </summary>
/// <remarks>
/// Hosting remains outside Core. Startup validates the scoped drain dependency graph and fails when the store or emitter
/// cannot be resolved. Each drain cycle then resolves the drain through a new scoped service provider so durable providers
/// that depend on scoped infrastructure, such as a host-owned EF Core <c>DbContext</c>, remain safe to use. Runtime changes
/// to <see cref="AsiBackboneGovernanceOutboxDrainWorkerOptions.Enabled" /> pause or resume new drain cycles without
/// terminating the hosted service.
/// </remarks>
public sealed class AsiBackboneGovernanceOutboxDrainHostedService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AsiBackboneGovernanceOutboxDrainWorkerOptions> optionsMonitor,
    ILogger<AsiBackboneGovernanceOutboxDrainHostedService> logger) : BackgroundService
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
    private readonly IOptionsMonitor<AsiBackboneGovernanceOutboxDrainWorkerOptions> optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
    private readonly ILogger<AsiBackboneGovernanceOutboxDrainHostedService> logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _ = scope.ServiceProvider.GetRequiredService<AsiBackboneGovernanceOutboxDrain>();
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

        AsiBackboneGovernanceOutboxDrainWorkerOptions options = optionsMonitor.CurrentValue;

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
            AsiBackboneGovernanceOutboxDrainWorkerOptions options = optionsMonitor.CurrentValue;

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
        AsiBackboneGovernanceOutboxDrainWorkerOptions options,
        CancellationToken cancellationToken)
    {
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        using IServiceScope scope = scopeFactory.CreateScope();
        AsiBackboneGovernanceOutboxDrain drain = scope.ServiceProvider.GetRequiredService<AsiBackboneGovernanceOutboxDrain>();
        DateTimeOffset retryUtc = options.RetryClock().ToUniversalTime();
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

    private static TaskCompletionSource CreateOptionsChangedSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

using AsiBackbone.AspNetCore.Outbox;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Outbox;

/// <summary>
/// Covers runtime reconfiguration, failure-delay, cancellation, and shutdown branches for the governance outbox worker.
/// </summary>
public sealed class AsiBackboneGovernanceOutboxDrainHostedServiceLifecycleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Verifies that a worker disabled at startup validates its dependencies without attempting a drain.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DisabledAtStartupValidatesDependenciesWithoutDraining()
    {
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions(enabled: false);
        using WorkerHarness harness = CreateHarness(options);

        await harness.Service.StartAsync(TestContext.Current.CancellationToken);
        await WaitAsync(harness.Logger.WaitForEventAsync(19803));
        await StopAsync(harness.Service);

        Assert.Equal(1, harness.ScopeFactory.CreateScopeCallCount);
        Assert.Equal(0, harness.Store.FindPendingCallCount);
        Assert.Contains(harness.Logger.Entries, entry => entry.EventId.Id == 19803);
    }

    /// <summary>
    /// Verifies that runtime disablement delays without draining and that re-enabling resumes drain passes.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task RuntimeDisableDelaysAndReenableResumesDraining()
    {
        AsiBackboneGovernanceOutboxDrainWorkerOptions initialOptions = CreateOptions();
        initialOptions.PollingInterval = TimeSpan.FromMilliseconds(50);
        using WorkerHarness harness = CreateHarness(initialOptions);

        await harness.Service.StartAsync(TestContext.Current.CancellationToken);
        await WaitAsync(harness.Store.WaitForFindPendingCallCountAsync(1));

        AsiBackboneGovernanceOutboxDrainWorkerOptions disabledOptions = CreateOptions(enabled: false);
        disabledOptions.PollingInterval = TimeSpan.FromMilliseconds(100);
        int disabledVersion = harness.Options.Set(disabledOptions);

        await WaitAsync(harness.Options.WaitForObservedVersionAsync(disabledVersion));
        Assert.Equal(1, harness.Store.FindPendingCallCount);

        AsiBackboneGovernanceOutboxDrainWorkerOptions enabledOptions = CreateOptions();
        enabledOptions.PollingInterval = TimeSpan.FromDays(1);
        _ = harness.Options.Set(enabledOptions);

        await WaitAsync(harness.Store.WaitForFindPendingCallCountAsync(2));
        await StopAsync(harness.Service);

        Assert.Equal(2, harness.Store.FindPendingCallCount);
    }

    /// <summary>
    /// Verifies that a missing scoped drain fails startup instead of entering the polling retry loop.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task MissingScopedDrainFailsStartupAndLogsCritical()
    {
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions();
        using WorkerHarness harness = CreateHarness(options, registerDrain: false);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, harness.ScopeFactory.CreateScopeCallCount);
        LogEntry entry = Assert.Single(harness.Logger.Entries, entry => entry.EventId.Id == 19806);
        Assert.Equal(LogLevel.Critical, entry.LogLevel);
        _ = Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    /// <summary>
    /// Verifies that cancellation during the normal polling delay stops the worker cleanly without another drain.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CancellationDuringPollingDelayStopsCleanly()
    {
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions();
        options.PollingInterval = TimeSpan.FromDays(1);
        using WorkerHarness harness = CreateHarness(options);

        await harness.Service.StartAsync(TestContext.Current.CancellationToken);
        await WaitAsync(harness.Store.WaitForFindPendingCallCountAsync(1));
        await StopAsync(harness.Service);

        Assert.Equal(1, harness.Store.FindPendingCallCount);
        Assert.DoesNotContain(harness.Logger.Entries, entry => entry.EventId.Id == 19805);
    }

    /// <summary>
    /// Verifies that cancellation during the worker failure delay stops the worker cleanly without another retry.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CancellationDuringFailureDelayStopsCleanly()
    {
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions();
        options.FailureDelay = TimeSpan.FromDays(1);
        var store = new RecordingOutboxStore((_, _) => throw new InvalidOperationException("Simulated store failure."));
        using WorkerHarness harness = CreateHarness(options, store);

        await harness.Service.StartAsync(TestContext.Current.CancellationToken);
        await WaitAsync(harness.Logger.WaitForEventAsync(19805));
        await StopAsync(harness.Service);

        Assert.Equal(1, harness.Store.FindPendingCallCount);
        Assert.Contains(harness.Logger.Entries, entry => entry.EventId.Id == 19805);
    }

    /// <summary>
    /// Verifies that retry-clock values are normalized to UTC before the drain queries retry-ready entries.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task RetryClockIsNormalizedToUtcBeforeDrainInvocation()
    {
        DateTimeOffset retryClock = new(2026, 7, 11, 18, 30, 0, TimeSpan.FromHours(5.5));
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions();
        options.RetryClock = () => retryClock;
        options.PollingInterval = TimeSpan.FromDays(1);
        using WorkerHarness harness = CreateHarness(options);

        await harness.Service.StartAsync(TestContext.Current.CancellationToken);
        DateTimeOffset observedRetryUtc = await WaitAsync(harness.Store.WaitForRetryReadyAsync());
        await StopAsync(harness.Service);

        Assert.Equal(TimeSpan.Zero, observedRetryUtc.Offset);
        Assert.Equal(retryClock.ToUniversalTime(), observedRetryUtc);
    }

    /// <summary>
    /// Verifies that shutdown draining is skipped when disabled, not requested, or the supplied stop token is already canceled.
    /// </summary>
    /// <param name="enabled">Whether the worker is enabled.</param>
    /// <param name="drainOnShutdown">Whether shutdown draining is requested.</param>
    /// <param name="cancelStopToken">Whether the stop token is canceled before <c>StopAsync</c>.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task ShutdownDrainIsSkippedWhenNotEligible(
        bool enabled,
        bool drainOnShutdown,
        bool cancelStopToken)
    {
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions(enabled);
        options.DrainOnShutdown = drainOnShutdown;
        using WorkerHarness harness = CreateHarness(options);
        using var stopCancellation = new CancellationTokenSource();

        if (cancelStopToken)
        {
            stopCancellation.Cancel();
        }

        await harness.Service.StopAsync(stopCancellation.Token);

        Assert.Equal(0, harness.ScopeFactory.CreateScopeCallCount);
        Assert.Equal(0, harness.Store.FindPendingCallCount);
    }

    /// <summary>
    /// Verifies that shutdown-drain timeout is swallowed and logged as cancellation rather than replacing host shutdown.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ShutdownDrainTimeoutIsSwallowedAndLogged()
    {
        var enteredDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new RecordingOutboxStore(async (attempt, cancellationToken) =>
        {
            _ = enteredDrain.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Array.Empty<GovernanceOutboxEntry>();
        });
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions();
        options.DrainOnShutdown = true;
        options.ShutdownDrainTimeout = TimeSpan.FromMilliseconds(200);
        using WorkerHarness harness = CreateHarness(options, store);

        Task stopTask = harness.Service.StopAsync(CancellationToken.None);
        await WaitAsync(enteredDrain.Task);
        await WaitAsync(stopTask);

        Assert.Equal(1, harness.ScopeFactory.CreateScopeCallCount);
        Assert.Contains(harness.Logger.Entries, entry => entry.EventId.Id == 19801 && entry.LogLevel == LogLevel.Debug);
    }

    /// <summary>
    /// Verifies that cancellation of the host stop token during shutdown drain is swallowed and logged.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ShutdownDrainCancellationIsSwallowedAndLogged()
    {
        var enteredDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new RecordingOutboxStore(async (attempt, cancellationToken) =>
        {
            _ = enteredDrain.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Array.Empty<GovernanceOutboxEntry>();
        });
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions();
        options.DrainOnShutdown = true;
        options.ShutdownDrainTimeout = TimeSpan.FromDays(1);
        using WorkerHarness harness = CreateHarness(options, store);
        using var stopCancellation = new CancellationTokenSource();

        Task stopTask = harness.Service.StopAsync(stopCancellation.Token);
        await WaitAsync(enteredDrain.Task);
        await stopCancellation.CancelAsync();
        await WaitAsync(stopTask);

        Assert.Equal(1, harness.ScopeFactory.CreateScopeCallCount);
        Assert.Contains(harness.Logger.Entries, entry => entry.EventId.Id == 19801 && entry.LogLevel == LogLevel.Debug);
    }

    /// <summary>
    /// Verifies that an unexpected shutdown-drain exception is swallowed and logged without replacing host shutdown.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task UnexpectedShutdownDrainFailureIsSwallowedAndLogged()
    {
        var failure = new InvalidOperationException("shutdown drain failed");
        var store = new RecordingOutboxStore((attempt, cancellationToken) => throw failure);
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions();
        options.DrainOnShutdown = true;
        using WorkerHarness harness = CreateHarness(options, store);

        await WaitAsync(harness.Service.StopAsync(CancellationToken.None));

        LogEntry logEntry = Assert.Single(harness.Logger.Entries, entry => entry.EventId.Id == 19802);
        Assert.Equal(LogLevel.Warning, logEntry.LogLevel);
        Assert.Same(failure, logEntry.Exception);
    }

    /// <summary>
    /// Verifies that one successful <c>StopAsync</c> call performs at most one shutdown drain pass.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task SuccessfulShutdownDrainRunsOncePerStopCall()
    {
        AsiBackboneGovernanceOutboxDrainWorkerOptions options = CreateOptions();
        options.DrainOnShutdown = true;
        using WorkerHarness harness = CreateHarness(options);

        await WaitAsync(harness.Service.StopAsync(CancellationToken.None));

        Assert.Equal(1, harness.ScopeFactory.CreateScopeCallCount);
        Assert.Equal(1, harness.Store.FindPendingCallCount);
        Assert.DoesNotContain(harness.Logger.Entries, entry => entry.EventId.Id is 19801 or 19802);
    }

    private static AsiBackboneGovernanceOutboxDrainWorkerOptions CreateOptions(bool enabled = true)
    {
        return new AsiBackboneGovernanceOutboxDrainWorkerOptions
        {
            Enabled = enabled,
            BatchSize = 1,
            PollingInterval = TimeSpan.FromDays(1),
            FailureDelay = TimeSpan.FromDays(1),
            RetryClock = static () => DateTimeOffset.UtcNow,
            ShutdownDrainTimeout = TimeSpan.FromSeconds(1),
        };
    }

    private static WorkerHarness CreateHarness(
        AsiBackboneGovernanceOutboxDrainWorkerOptions options,
        RecordingOutboxStore? store = null,
        bool registerDrain = true)
    {
        store ??= new RecordingOutboxStore();
        ServiceCollection services = new();
        _ = services.AddLogging();

        if (registerDrain)
        {
            _ = services.AddSingleton<IAsiBackboneGovernanceOutboxStore>(store);
            _ = services.AddSingleton<IAsiBackboneGovernanceEmitter>(NoOpGovernanceEmitter.Instance);
            _ = services.AddSingleton(Options.Create(new AsiBackboneGovernanceOutboxOptions { UseClaimLeases = false }));
            _ = services.AddScoped<AsiBackboneGovernanceOutboxDrain>();
        }

        ServiceProvider provider = services.BuildServiceProvider();
        var scopeFactory = new RecordingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
        var optionsMonitor = new ControllableOptionsMonitor<AsiBackboneGovernanceOutboxDrainWorkerOptions>(options);
        var logger = new RecordingLogger<AsiBackboneGovernanceOutboxDrainHostedService>();
        var service = new AsiBackboneGovernanceOutboxDrainHostedService(scopeFactory, optionsMonitor, logger);

        return new WorkerHarness(provider, service, scopeFactory, optionsMonitor, logger, store);
    }

    private static async Task StopAsync(AsiBackboneGovernanceOutboxDrainHostedService service)
    {
        await WaitAsync(service.StopAsync(CancellationToken.None));
    }

    private static async Task WaitAsync(Task task)
    {
        await task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    private static async Task<T> WaitAsync<T>(Task<T> task)
    {
        return await task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    private sealed class WorkerHarness(
        ServiceProvider provider,
        AsiBackboneGovernanceOutboxDrainHostedService service,
        RecordingScopeFactory scopeFactory,
        ControllableOptionsMonitor<AsiBackboneGovernanceOutboxDrainWorkerOptions> options,
        RecordingLogger<AsiBackboneGovernanceOutboxDrainHostedService> logger,
        RecordingOutboxStore store) : IDisposable
    {
        public AsiBackboneGovernanceOutboxDrainHostedService Service { get; } = service;

        public RecordingScopeFactory ScopeFactory { get; } = scopeFactory;

        public ControllableOptionsMonitor<AsiBackboneGovernanceOutboxDrainWorkerOptions> Options { get; } = options;

        public RecordingLogger<AsiBackboneGovernanceOutboxDrainHostedService> Logger { get; } = logger;

        public RecordingOutboxStore Store { get; } = store;

        public void Dispose()
        {
            Service.Dispose();
            provider.Dispose();
        }
    }

    private sealed class ControllableOptionsMonitor<TOptions>(TOptions initialValue) : IOptionsMonitor<TOptions>
        where TOptions : class
    {
        private readonly Lock sync = new();
        private readonly List<ObservedVersionWaiter> observedVersionWaiters = [];
        private int currentVersion;
        private int observedVersion = -1;

        public TOptions CurrentValue
        {
            get
            {
                lock (sync)
                {
                    observedVersion = Math.Max(observedVersion, currentVersion);
                    CompleteObservedVersionWaiters();
                    return field;
                }
            }

            private set;
        } = initialValue;

        public TOptions Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<TOptions, string?> listener)
        {
            ArgumentNullException.ThrowIfNull(listener);
            return NoopDisposable.Instance;
        }

        public int Set(TOptions value)
        {
            ArgumentNullException.ThrowIfNull(value);

            lock (sync)
            {
                CurrentValue = value;
                return ++currentVersion;
            }
        }

        public Task WaitForObservedVersionAsync(int version)
        {
            lock (sync)
            {
                if (observedVersion >= version)
                {
                    return Task.CompletedTask;
                }

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                observedVersionWaiters.Add(new ObservedVersionWaiter(version, completion));
                return completion.Task;
            }
        }

        private void CompleteObservedVersionWaiters()
        {
            for (int index = observedVersionWaiters.Count - 1; index >= 0; index--)
            {
                ObservedVersionWaiter waiter = observedVersionWaiters[index];
                if (observedVersion >= waiter.Version)
                {
                    observedVersionWaiters.RemoveAt(index);
                    _ = waiter.Completion.TrySetResult();
                }
            }
        }

        private sealed record ObservedVersionWaiter(int Version, TaskCompletionSource Completion);
    }

    private sealed class RecordingScopeFactory(IServiceScopeFactory innerScopeFactory) : IServiceScopeFactory
    {
        private readonly Lock sync = new();
        private readonly List<ScopeCountWaiter> scopeCountWaiters = [];
        private int createScopeCallCount;

        public int CreateScopeCallCount => Volatile.Read(ref createScopeCallCount);

        public IServiceScope CreateScope()
        {
            int count = Interlocked.Increment(ref createScopeCallCount);

            lock (sync)
            {
                for (int index = scopeCountWaiters.Count - 1; index >= 0; index--)
                {
                    ScopeCountWaiter waiter = scopeCountWaiters[index];
                    if (count >= waiter.ExpectedCount)
                    {
                        scopeCountWaiters.RemoveAt(index);
                        _ = waiter.Completion.TrySetResult();
                    }
                }
            }

            return innerScopeFactory.CreateScope();
        }

        public Task WaitForCreateScopeCallCountAsync(int expectedCount)
        {
            if (CreateScopeCallCount >= expectedCount)
            {
                return Task.CompletedTask;
            }

            lock (sync)
            {
                if (CreateScopeCallCount >= expectedCount)
                {
                    return Task.CompletedTask;
                }

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                scopeCountWaiters.Add(new ScopeCountWaiter(expectedCount, completion));
                return completion.Task;
            }
        }

        private sealed record ScopeCountWaiter(int ExpectedCount, TaskCompletionSource Completion);
    }

    private sealed class RecordingOutboxStore(
        Func<int, CancellationToken, ValueTask<IReadOnlyList<GovernanceOutboxEntry>>>? findPending = null) : IAsiBackboneGovernanceOutboxStore
    {
        private readonly Lock sync = new();
        private readonly Func<int, CancellationToken, ValueTask<IReadOnlyList<GovernanceOutboxEntry>>> findPending = findPending ?? EmptyPendingAsync;
        private readonly List<PendingCountWaiter> pendingCountWaiters = [];
        private readonly TaskCompletionSource<DateTimeOffset> retryReadyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int findPendingCallCount;
        private DateTimeOffset? lastRetryReadyUtc;

        public int FindPendingCallCount => Volatile.Read(ref findPendingCallCount);

        public ValueTask<GovernanceOutboxEntry> EnqueueAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> SaveAsync(
            GovernanceOutboxEntry entry,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(
            string outboxEntryId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            int count = Interlocked.Increment(ref findPendingCallCount);

            lock (sync)
            {
                for (int index = pendingCountWaiters.Count - 1; index >= 0; index--)
                {
                    PendingCountWaiter waiter = pendingCountWaiters[index];
                    if (count >= waiter.ExpectedCount)
                    {
                        pendingCountWaiters.RemoveAt(index);
                        _ = waiter.Completion.TrySetResult();
                    }
                }
            }

            return findPending(count, cancellationToken);
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
            DateTimeOffset utcNow,
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (sync)
            {
                lastRetryReadyUtc = utcNow;
                _ = retryReadyCompletion.TrySetResult(utcNow);
            }

            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(Array.Empty<GovernanceOutboxEntry>());
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(
            string outboxEntryId,
            GovernanceEmissionResult result,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkFailedAsync(
            string outboxEntryId,
            GovernanceEmissionError governanceEmissionError,
            DateTimeOffset? nextRetryUtc = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(
            string outboxEntryId,
            GovernanceEmissionError governanceEmissionError,
            string? deadLetterReason = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task WaitForFindPendingCallCountAsync(int expectedCount)
        {
            if (FindPendingCallCount >= expectedCount)
            {
                return Task.CompletedTask;
            }

            lock (sync)
            {
                if (FindPendingCallCount >= expectedCount)
                {
                    return Task.CompletedTask;
                }

                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                pendingCountWaiters.Add(new PendingCountWaiter(expectedCount, completion));
                return completion.Task;
            }
        }

        public Task<DateTimeOffset> WaitForRetryReadyAsync()
        {
            lock (sync)
            {
                return lastRetryReadyUtc.HasValue
                    ? Task.FromResult(lastRetryReadyUtc.Value)
                    : retryReadyCompletion.Task;
            }
        }

        private static ValueTask<IReadOnlyList<GovernanceOutboxEntry>> EmptyPendingAsync(
            int attempt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(Array.Empty<GovernanceOutboxEntry>());
        }

        private sealed record PendingCountWaiter(int ExpectedCount, TaskCompletionSource Completion);
    }

    private sealed class NoOpGovernanceEmitter : IAsiBackboneGovernanceEmitter
    {
        public static NoOpGovernanceEmitter Instance { get; } = new();

        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GovernanceEmissionResult.Delivered("test-sink"));
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly Lock sync = new();
        private readonly List<LogEntry> entries = [];
        private readonly List<LogWaiter> logWaiters = [];

        public Task WaitForEventAsync(int eventId)
        {
            lock (sync)
            {
                foreach (LogEntry entry in entries)
                {
                    if (entry.EventId.Id == eventId)
                    {
                        return Task.CompletedTask;
                    }
                }

                var completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                logWaiters.Add(new LogWaiter(eventId, completion));
                return completion.Task;
            }
        }

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (sync)
                {
                    return [.. entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NoopDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            lock (sync)
            {
                entries.Add(
                    new LogEntry(
                        logLevel,
                        eventId,
                        exception,
                        formatter(state, exception)));

                for (int index = logWaiters.Count - 1; index >= 0; index--)
                {
                    LogWaiter waiter = logWaiters[index];

                    if (waiter.EventId == eventId.Id)
                    {
                        logWaiters.RemoveAt(index);
                        _ = waiter.Completion.TrySetResult();
                    }
                }
            }
        }

        private sealed record LogWaiter(
            int EventId,
            TaskCompletionSource Completion);
    }

    private sealed record LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        Exception? Exception,
        string Message);

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

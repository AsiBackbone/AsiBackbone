using AsiBackbone.AspNetCore.Outbox;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Outbox;

/// <summary>
/// Covers runtime enable, disable, re-enable, and shutdown behavior for the hosted governance outbox drain.
/// </summary>
public sealed class AsiBackboneGovernanceOutboxDrainRuntimeOptionsTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Verifies that a startup-disabled worker remains alive, reacts to option changes, pauses without creating scopes, and resumes without a process restart.
    /// </summary>
    [Fact]
    public async Task StartupDisabledWorkerCanEnableDisableAndReenableAtRuntime()
    {
        AsiBackboneGovernanceOutboxDrainWorkerOptions disabled = CreateOptions(enabled: false);
        using RuntimeHarness harness = CreateHarness(disabled);

        await harness.Service.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.ScopeFactory.CreateScopeCallCount);
        Assert.Equal(0, harness.Store.FindPendingCallCount);

        harness.Options.Set(CreateOptions(enabled: true));
        await harness.Store.WaitForFindPendingCallCountAsync(1)
            .WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

        harness.Options.Set(CreateOptions(enabled: false));
        int pausedScopeCount = harness.ScopeFactory.CreateScopeCallCount;
        int pausedFindPendingCount = harness.Store.FindPendingCallCount;

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        Assert.Equal(pausedScopeCount, harness.ScopeFactory.CreateScopeCallCount);
        Assert.Equal(pausedFindPendingCount, harness.Store.FindPendingCallCount);

        harness.Options.Set(CreateOptions(enabled: true));
        await harness.Store.WaitForFindPendingCallCountAsync(pausedFindPendingCount + 1)
            .WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

        await harness.Service.StopAsync(CancellationToken.None)
            .WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

        Assert.True(harness.Store.FindPendingCallCount >= 2);
    }

    /// <summary>
    /// Verifies that shutdown promptly cancels a startup-disabled worker waiting on a long polling interval.
    /// </summary>
    [Fact]
    public async Task StartupDisabledWorkerStopsPromptlyDuringDisabledWait()
    {
        using RuntimeHarness harness = CreateHarness(CreateOptions(enabled: false));

        await harness.Service.StartAsync(TestContext.Current.CancellationToken);
        await harness.Service.StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.ScopeFactory.CreateScopeCallCount);
    }

    private static AsiBackboneGovernanceOutboxDrainWorkerOptions CreateOptions(bool enabled)
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

    private static RuntimeHarness CreateHarness(AsiBackboneGovernanceOutboxDrainWorkerOptions initialOptions)
    {
        var store = new RecordingOutboxStore();
        ServiceCollection services = new();
        _ = services.AddSingleton<IAsiBackboneGovernanceOutboxStore>(store);
        _ = services.AddSingleton<IAsiBackboneGovernanceEmitter>(NoOpGovernanceEmitter.Instance);
        _ = services.AddSingleton(Options.Create(new AsiBackboneGovernanceOutboxOptions { UseClaimLeases = false }));
        _ = services.AddScoped<AsiBackboneGovernanceOutboxDrain>();

        ServiceProvider provider = services.BuildServiceProvider();
        var scopeFactory = new RecordingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
        var options = new RuntimeOptionsMonitor(initialOptions);
        var service = new AsiBackboneGovernanceOutboxDrainHostedService(
            scopeFactory,
            options,
            NullLogger<AsiBackboneGovernanceOutboxDrainHostedService>.Instance);

        return new RuntimeHarness(provider, service, scopeFactory, options, store);
    }

    private sealed class RuntimeHarness(
        ServiceProvider provider,
        AsiBackboneGovernanceOutboxDrainHostedService service,
        RecordingScopeFactory scopeFactory,
        RuntimeOptionsMonitor options,
        RecordingOutboxStore store) : IDisposable
    {
        public AsiBackboneGovernanceOutboxDrainHostedService Service { get; } = service;

        public RecordingScopeFactory ScopeFactory { get; } = scopeFactory;

        public RuntimeOptionsMonitor Options { get; } = options;

        public RecordingOutboxStore Store { get; } = store;

        public void Dispose()
        {
            Service.Dispose();
            provider.Dispose();
        }
    }

    private sealed class RuntimeOptionsMonitor(AsiBackboneGovernanceOutboxDrainWorkerOptions initialValue)
        : IOptionsMonitor<AsiBackboneGovernanceOutboxDrainWorkerOptions>
    {
        private readonly Lock sync = new();
        private readonly List<Action<AsiBackboneGovernanceOutboxDrainWorkerOptions, string?>> listeners = [];

        public AsiBackboneGovernanceOutboxDrainWorkerOptions CurrentValue
        {
            get
            {
                lock (sync)
                {
                    return field;
                }
            }

            private set;
        } = initialValue;

        public AsiBackboneGovernanceOutboxDrainWorkerOptions Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable OnChange(Action<AsiBackboneGovernanceOutboxDrainWorkerOptions, string?> listener)
        {
            ArgumentNullException.ThrowIfNull(listener);

            lock (sync)
            {
                listeners.Add(listener);
            }

            return new CallbackDisposable(() =>
            {
                lock (sync)
                {
                    _ = listeners.Remove(listener);
                }
            });
        }

        public void Set(AsiBackboneGovernanceOutboxDrainWorkerOptions value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Action<AsiBackboneGovernanceOutboxDrainWorkerOptions, string?>[] callbacks;

            lock (sync)
            {
                CurrentValue = value;
                callbacks = [.. listeners];
            }

            foreach (Action<AsiBackboneGovernanceOutboxDrainWorkerOptions, string?> callback in callbacks)
            {
                callback(value, Options.DefaultName);
            }
        }
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private Action? callback = callback;

        public void Dispose()
        {
            Interlocked.Exchange(ref callback, null)?.Invoke();
        }
    }

    private sealed class RecordingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private readonly Lock sync = new();
        private readonly List<(int ExpectedCount, TaskCompletionSource Completion)> waiters = [];
        private int createScopeCallCount;

        public int CreateScopeCallCount => Volatile.Read(ref createScopeCallCount);

        public IServiceScope CreateScope()
        {
            int count = Interlocked.Increment(ref createScopeCallCount);

            lock (sync)
            {
                for (int index = waiters.Count - 1; index >= 0; index--)
                {
                    if (count >= waiters[index].ExpectedCount)
                    {
                        _ = waiters[index].Completion.TrySetResult();
                        waiters.RemoveAt(index);
                    }
                }
            }

            return inner.CreateScope();
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
                waiters.Add((expectedCount, completion));
                return completion.Task;
            }
        }
    }

    private sealed class RecordingOutboxStore : IAsiBackboneGovernanceOutboxStore
    {
        private readonly Lock sync = new();
        private readonly List<(int ExpectedCount, TaskCompletionSource Completion)> waiters = [];
        private int findPendingCallCount;

        public int FindPendingCallCount => Volatile.Read(ref findPendingCallCount);

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

                var completion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                waiters.Add((expectedCount, completion));
                return completion.Task;
            }
        }

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
            cancellationToken.ThrowIfCancellationRequested();

            int count = Interlocked.Increment(ref findPendingCallCount);

            lock (sync)
            {
                for (int index = waiters.Count - 1; index >= 0; index--)
                {
                    if (count >= waiters[index].ExpectedCount)
                    {
                        _ = waiters[index].Completion.TrySetResult();
                        waiters.RemoveAt(index);
                    }
                }
            }

            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(
                Array.Empty<GovernanceOutboxEntry>());
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
            DateTimeOffset utcNow,
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            return ValueTask.FromResult(GovernanceEmissionResult.Delivered("runtime-options-test"));
        }
    }
}

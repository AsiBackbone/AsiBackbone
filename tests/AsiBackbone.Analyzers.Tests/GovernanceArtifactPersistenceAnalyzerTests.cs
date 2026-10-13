using System.Collections.Immutable;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Results;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AsiBackbone.Analyzers.Tests;

/// <summary>
/// Tests for the <see cref="GovernanceArtifactPersistenceAnalyzer"/> analyzer.
/// </summary>
public sealed class GovernanceArtifactPersistenceAnalyzerTests
{
    /// <summary>
    /// Tests that a discarded <see cref="GovernanceDecision"/> reports the ASIB001 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DiscardedGovernanceDecisionReportsASIB001()
    {
        string source = SourceWithBody("GovernanceDecision.Allow();");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(GovernanceArtifactPersistenceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that a discarded assignment of a <see cref="GovernanceDecision"/> reports the ASIB001 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DiscardAssignmentReportsASIB001()
    {
        string source = SourceWithBody("_ = GovernanceDecision.Allow();");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(GovernanceArtifactPersistenceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that a stored <see cref="GovernanceDecision"/> does not report any diagnostics.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task StoredGovernanceDecisionDoesNotReport()
    {
        string source = SourceWithBody("GovernanceDecision decision = GovernanceDecision.Allow(); _ = decision;");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests that a returned <see cref="GovernanceDecision"/> does not report any diagnostics.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ReturnedGovernanceDecisionDoesNotReport()
    {
        string source = """
            using AsiBackbone.Core.Decisions;

            public static class Sample
            {
                public static GovernanceDecision Create()
                {
                    return GovernanceDecision.Allow();
                }
            }

            namespace AsiBackbone.Core.Decisions
            {
                public sealed class GovernanceDecision
                {
                    public static GovernanceDecision Allow() => new();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests that an awaited <see cref="GovernanceDecision"/> reports the ASIB001 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task AwaitedGovernanceDecisionReportsASIB001()
    {
        string source = """
            using System.Threading.Tasks;
            using AsiBackbone.Core.Decisions;

            public static class Sample
            {
                public static async Task ExecuteAsync()
                {
                    await Task.FromResult(GovernanceDecision.Allow());
                }
            }

            namespace AsiBackbone.Core.Decisions
            {
                public sealed class GovernanceDecision
                {
                    public static GovernanceDecision Allow() => new();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(GovernanceArtifactPersistenceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that awaiting an artifact-returning persistence store call does not report a diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task AwaitedPersistenceStoreCallDoesNotReport()
    {
        string source = """
            using System.Threading.Tasks;
            using AsiBackbone.Core.Audit;
            using AsiBackbone.Core.Results;

            public static class Sample
            {
                public static async Task ExecuteAsync(AuditLedgerStore store, AuditLedgerRecord record)
                {
                    await store.AppendAsync(record);
                }
            }

            namespace AsiBackbone.Core.Audit
            {
                public sealed class AuditLedgerRecord;

                public interface IGovernanceAuditLedgerStore
                {
                    ValueTask<OperationResult<AuditLedgerRecord>> AppendAsync(AuditLedgerRecord record);
                }

                public sealed class AuditLedgerStore : IGovernanceAuditLedgerStore
                {
                    public ValueTask<OperationResult<AuditLedgerRecord>> AppendAsync(AuditLedgerRecord record) => default;
                }
            }

            namespace AsiBackbone.Core.Results
            {
                public sealed class OperationResult<T>;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests that assigning an unawaited artifact-producing operation still reports the ASIB001 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task UnawaitedArtifactProducerAssignmentReportsASIB001()
    {
        string source = """
            using System.Threading.Tasks;
            using AsiBackbone.Core.Decisions;
            using AsiBackbone.Core.Evaluation;

            public sealed class Sample
            {
                private ValueTask<GovernanceDecision> pendingDecision;

                public void Execute(IGovernancePolicyEvaluator evaluator)
                {
                    pendingDecision = evaluator.EvaluateAsync();
                }
            }

            namespace AsiBackbone.Core.Decisions
            {
                public sealed class GovernanceDecision;
            }

            namespace AsiBackbone.Core.Evaluation
            {
                public interface IGovernancePolicyEvaluator
                {
                    ValueTask<GovernanceDecision> EvaluateAsync();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(GovernanceArtifactPersistenceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that an <see cref="OperationResult{T}"/> of a <see cref="GovernanceDecision"/> reports the ASIB001 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task OperationResultOfGovernanceDecisionReportsASIB001()
    {
        string source = """
            using AsiBackbone.Core.Decisions;
            using AsiBackbone.Core.Results;

            public static class Sample
            {
                public static void Execute()
                {
                    OperationResult<GovernanceDecision>.Success(GovernanceDecision.Allow());
                }
            }

            namespace AsiBackbone.Core.Decisions
            {
                public sealed class GovernanceDecision
                {
                    public static GovernanceDecision Allow() => new();
                }
            }

            namespace AsiBackbone.Core.Results
            {
                public sealed class OperationResult<T>
                {
                    public static OperationResult<T> Success(T value) => new();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(GovernanceArtifactPersistenceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that a method or class marked with the <c>AsiBackbonePersistenceHandledAttribute</c> suppresses the ASIB001 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task HostMarkerAttributeSuppressesASIB001()
    {
        string source = """
            using System;
            using AsiBackbone.Core.Decisions;

            [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
            internal sealed class AsiBackbonePersistenceHandledAttribute : Attribute;

            public static class Sample
            {
                [AsiBackbonePersistenceHandled]
                public static void Execute()
                {
                    GovernanceDecision.Allow();
                }
            }

            namespace AsiBackbone.Core.Decisions
            {
                public sealed class GovernanceDecision
                {
                    public static GovernanceDecision Allow() => new();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests the producer, wrapper, and persistence-boundary shapes the analyzer distinguishes.
    /// </summary>
    /// <param name="body">The method body under analysis.</param>
    /// <param name="expectDiagnostic">Whether ASIB001 is expected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("_ = flag ? GovernanceDecision.Allow() : GovernanceDecision.Allow();", true)]
    [InlineData("_ = maybe ?? GovernanceDecision.Allow();", true)]
    [InlineData("_ = maybe ?? existing;", false)]
    [InlineData("_ = maybe;", false)]
    [InlineData("_ = GovernanceDecision.Current;", true)]
    [InlineData("_ = new GovernanceDecision();", true)]
    [InlineData("_ = (GovernanceDecision)new DerivedDecision();", true)]
    [InlineData("store.Save();", false)]
    [InlineData("concreteStore.Save();", false)]
    [InlineData("factory.Create();", true)]
    [InlineData("await Task.FromResult(GovernanceDecision.Allow());", true)]
    [InlineData("_ = new ValueTask<GovernanceDecision>(GovernanceDecision.Allow());", true)]
    [InlineData("_ = new Lazy<GovernanceDecision>();", false)]
    [InlineData("_ = Task.FromResult(1);", false)]
    [InlineData("int count = 1; _ = count;", false)]
    [InlineData("Dictionary<int, int> map; map = new Dictionary<int, int>();", false)]
    [InlineData("List<int> list; list = new List<int>();", false)]
    [InlineData("Task task; task = Task.CompletedTask;", false)]
    [InlineData("ValueTask<int> pending; pending = new ValueTask<int>(1);", false)]
    [InlineData("ValueTask<GovernanceDecision> pending; pending = new ValueTask<GovernanceDecision>(GovernanceDecision.Allow());", true)]
    public async Task ArtifactProducerShapesReportExpectedDiagnostic(string body, bool expectDiagnostic)
    {
        string source = $$"""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using AsiBackbone.Core.Decisions;
            using AsiBackbone.Core.Outbox;

            public static class Sample
            {
                public static async Task Execute(
                    bool flag,
                    GovernanceDecision? maybe,
                    GovernanceDecision existing,
                    IGovernanceOutboxStore store,
                    ConcreteOutboxStore concreteStore,
                    DecisionFactory factory)
                {
                    {{body}}
                    await Task.CompletedTask;
                }
            }

            public sealed class DecisionFactory
            {
                public GovernanceDecision Create() => new();
            }

            public sealed class DerivedDecision : GovernanceDecision;

            public sealed class ConcreteOutboxStore : IGovernanceOutboxStore
            {
                public GovernanceDecision Save() => new();
            }

            namespace AsiBackbone.Core.Decisions
            {
                public class GovernanceDecision
                {
                    public static GovernanceDecision Current { get; } = new();

                    public static GovernanceDecision Allow() => new();
                }
            }

            namespace AsiBackbone.Core.Outbox
            {
                public interface IGovernanceOutboxStore
                {
                    AsiBackbone.Core.Decisions.GovernanceDecision Save();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        if (expectDiagnostic)
        {
            Diagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal(GovernanceArtifactPersistenceAnalyzer.DiagnosticId, diagnostic.Id);
        }
        else
        {
            Assert.Empty(diagnostics);
        }
    }

    /// <summary>
    /// Tests that a type-level marker attribute declared without the Attribute suffix suppresses the ASIB001 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task TypeLevelHostMarkerWithoutAttributeSuffixSuppressesASIB001()
    {
        string source = """
            using System;
            using AsiBackbone.Core.Decisions;

            [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
            internal sealed class AsiBackbonePersistenceHandled : Attribute;

            [AsiBackbonePersistenceHandled]
            public static class Sample
            {
                public static void Execute()
                {
                    GovernanceDecision.Allow();
                    _ = GovernanceDecision.Allow();
                }
            }

            namespace AsiBackbone.Core.Decisions
            {
                public sealed class GovernanceDecision
                {
                    public static GovernanceDecision Allow() => new();
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    private static string SourceWithBody(string body)
    {
        return $$"""
            using AsiBackbone.Core.Decisions;

            public static class Sample
            {
                public static void Execute()
                {
                    {{body}}
                }
            }

            namespace AsiBackbone.Core.Decisions
            {
                public sealed class GovernanceDecision
                {
                    public static GovernanceDecision Allow() => new();
                }
            }
            """;
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview));

        var compilation = CSharpCompilation.Create(
            "AnalyzerTestAssembly",
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Diagnostic[] compilerErrors = [.. compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

        Assert.Empty(compilerErrors);

        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            [new GovernanceArtifactPersistenceAnalyzer()]);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static PortableExecutableReference[] GetMetadataReferences()
    {
        string trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies were not available for analyzer test compilation.");

        return [.. trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))];
    }
}

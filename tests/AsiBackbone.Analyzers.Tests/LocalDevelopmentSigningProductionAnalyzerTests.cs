using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AsiBackbone.Analyzers.Tests;

/// <summary>
/// Tests for the <see cref="LocalDevelopmentSigningProductionAnalyzer"/> analyzer, which checks for the usage of local development signing services in production code.
/// </summary>
public sealed class LocalDevelopmentSigningProductionAnalyzerTests
{
    /// <summary>
    /// Tests that registering the <c>LocalDevelopmentSigningService</c> inside a production branch reports the ASIB002 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task LocalDevelopmentSignerRegisteredInsideProductionBranchReportsASIB002()
    {
        string source = SourceWithProductionBody("services.AddSingleton<LocalDevelopmentSigningService>();");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(LocalDevelopmentSigningProductionAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that constructing the <c>LocalDevelopmentSigningService</c> inside a production branch reports the ASIB002 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task LocalDevelopmentSignerConstructedInsideProductionBranchReportsASIB002()
    {
        string source = SourceWithProductionBody("_ = new LocalDevelopmentSigningService();");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(LocalDevelopmentSigningProductionAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that registering the <c>LocalDevelopmentSigningOptions</c> inside a production branch reports the ASIB002 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task LocalDevelopmentOptionsRegisteredInsideProductionBranchReportsASIB002()
    {
        string source = SourceWithProductionBody("services.AddSingleton(LocalDevelopmentSigningOptions.Create());");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(LocalDevelopmentSigningProductionAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that registering the <c>LocalDevelopmentSigningService</c> inside a development branch does not report the ASIB002 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task LocalDevelopmentSignerRegisteredInsideDevelopmentBranchDoesNotReport()
    {
        string source = SourceWithBody("""
            if (environment.IsDevelopment())
            {
                services.AddSingleton<LocalDevelopmentSigningService>();
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests that comparing the environment name to "Production" reports the ASIB002 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ProductionEnvironmentNameComparisonReportsASIB002()
    {
        string source = SourceWithBody("""
            if (environment.EnvironmentName == "Production")
            {
                services.AddSingleton<LocalDevelopmentSigningService>();
            }
            """);

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(LocalDevelopmentSigningProductionAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that the <c>AsiBackboneProductionConfigurationReviewedAttribute</c> suppresses the ASIB002 diagnostic.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ProductionConfigurationReviewedMarkerSuppressesASIB002()
    {
        string source = SourceWithBody("""
            [AsiBackboneProductionConfigurationReviewed]
            static void Configure(IServiceCollection services, IHostEnvironment environment)
            {
                if (environment.IsProduction())
                {
                    services.AddSingleton<LocalDevelopmentSigningService>();
                }
            }
            """, includeConfigureWrapper: false);

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests that registering the signer with no environment guard reports the ASIB003 diagnostic.
    /// </summary>
    /// <remarks>
    /// ASIB002 only sees a call inside a production branch, so the unconditional registration that actually reaches
    /// production went unreported.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task LocalDevelopmentSignerRegisteredWithoutAnEnvironmentGuardReportsASIB003()
    {
        string source = SourceWithBody("services.AddSingleton<LocalDevelopmentSigningService>();");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that a registration guarded by a non-production environment check reports nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task LocalDevelopmentSignerRegisteredInsideDevelopmentBranchReportsNothing()
    {
        string source = SourceWithBody("""
            if (environment.IsDevelopment())
                    {
                        services.AddSingleton<LocalDevelopmentSigningService>();
                    }
            """);

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests that the <c>AsiBackboneProductionConfigurationReviewedAttribute</c> suppresses the ASIB003 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ProductionConfigurationReviewedMarkerSuppressesASIB003()
    {
        string source = SourceWithBody("""
            [AsiBackboneProductionConfigurationReviewed]
            static void Configure(IServiceCollection services, IHostEnvironment environment)
            {
                services.AddSingleton<LocalDevelopmentSigningService>();
            }
            """, includeConfigureWrapper: false);

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests the environment-condition shapes the analyzer recognizes as production, non-production, or unguarded.
    /// </summary>
    /// <param name="body">The configuration method body.</param>
    /// <param name="expectedDiagnosticId">The expected diagnostic ID, or an empty string when nothing should be reported.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("if (\"Production\" == environment.EnvironmentName) { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.DiagnosticId)]
    [InlineData("if (environment.EnvironmentName.Equals(\"Production\")) { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.DiagnosticId)]
    [InlineData("if (string.Equals(environment.EnvironmentName, \"production\", StringComparison.OrdinalIgnoreCase)) { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.DiagnosticId)]
    [InlineData("if (HostingEnvironmentExtensions.IsProduction(environment)) { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.DiagnosticId)]
    [InlineData("_ = environment.IsProduction() ? services.AddSingleton<LocalDevelopmentSigningService>() : services;", LocalDevelopmentSigningProductionAnalyzer.DiagnosticId)]
    [InlineData("if (environment.IsProduction()) { _ = new object(); }", "")]
    [InlineData("if (environment.EnvironmentName == \"Staging\") { services.AddSingleton<LocalDevelopmentSigningService>(); }", "")]
    [InlineData("if (environment.EnvironmentName != \"Production\") { services.AddSingleton<LocalDevelopmentSigningService>(); }", "")]
    [InlineData("if (environment.EnvironmentName.Equals(\"Staging\")) { services.AddSingleton<LocalDevelopmentSigningService>(); }", "")]
    [InlineData("if (!environment.IsDevelopment()) { services.AddSingleton<LocalDevelopmentSigningService>(); }", "")]
    [InlineData("if (HostFlags.UseSharedEnvironment) { services.AddSingleton<LocalDevelopmentSigningService>(); }", "")]
    [InlineData("if (HostFlags.Enabled) { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId)]
    [InlineData("if (environment.Equals(null)) { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId)]
    [InlineData("if (string.IsNullOrEmpty(environment.EnvironmentName)) { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId)]
    [InlineData("if (environment.EnvironmentName.Length > 0) { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId)]
    [InlineData("if (environment.EnvironmentName is \"Staging\") { services.AddSingleton<LocalDevelopmentSigningService>(); }", LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId)]
    [InlineData("services.AddSingleton(LocalDevelopmentSigningOptions.Create());", LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId)]
    [InlineData("services.AddSingleton<System.Collections.Generic.List<LocalDevelopmentSigningService>>();", LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId)]
    [InlineData("_ = SignerFactory.Create();", LocalDevelopmentSigningProductionAnalyzer.UnguardedDiagnosticId)]
    [InlineData("services.AddSingleton<System.Collections.Generic.List<string>>();", "")]
    [InlineData("services.AddSingleton(new int[0]);", "")]
    [InlineData("_ = new LocalDevelopmentSigningService();", "")]
    public async Task EnvironmentConditionShapesReportExpectedDiagnostic(string body, string expectedDiagnosticId)
    {
        string source = SourceWithBody(body) + HostFlagsSource;

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        if (expectedDiagnosticId.Length == 0)
        {
            Assert.Empty(diagnostics);
        }
        else
        {
            Diagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal(expectedDiagnosticId, diagnostic.Id);
        }
    }

    /// <summary>
    /// Tests that the review marker on the containing type suppresses diagnostics for its members.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ProductionConfigurationReviewedMarkerOnContainingTypeSuppressesDiagnostics()
    {
        string source = SourceWithBody("""
            public static void Configure(IServiceCollection services, IHostEnvironment environment)
            {
                services.AddSingleton<LocalDevelopmentSigningService>();
                if (environment.IsProduction())
                {
                    _ = new LocalDevelopmentSigningService();
                }
            }
            """, includeConfigureWrapper: false).Replace(
                "public static class Sample",
                "[AsiBackboneProductionConfigurationReviewed]\npublic static class Sample",
                StringComparison.Ordinal);

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    private const string HostFlagsSource = """

        public static class HostFlags
        {
            public static bool Enabled => true;

            public static bool UseSharedEnvironment => false;
        }

        public static class SignerFactory
        {
            public static LocalDevelopmentSigningService Create() => null!;
        }
        """;

    private static string SourceWithProductionBody(string body)
    {
        return SourceWithBody($$"""
            if (environment.IsProduction())
            {
                {{body}}
            }
            """);
    }

    private static string SourceWithBody(string body, bool includeConfigureWrapper = true)
    {
        string configureCode = includeConfigureWrapper
            ? $$"""
                public static class Sample
                {
                    public static void Configure(IServiceCollection services, IHostEnvironment environment)
                    {
                        {{body}}
                    }
                }
                """
            : $$"""
                public static class Sample
                {
                    {{body}}
                }
                """;

        return $$"""
            using System;
            using AsiBackbone.Signing.LocalDevelopment;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;

            [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
            internal sealed class AsiBackboneProductionConfigurationReviewedAttribute : Attribute;

            {{configureCode}}

            namespace AsiBackbone.Signing.LocalDevelopment
            {
                public sealed class LocalDevelopmentSigningService
                {
                }

                public sealed class LocalDevelopmentSigningOptions
                {
                    public static LocalDevelopmentSigningOptions Create() => new();
                }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection
                {
                }

                public static class ServiceCollectionServiceExtensions
                {
                    public static IServiceCollection AddSingleton<TService>(this IServiceCollection services) => services;

                    public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, TService instance) => services;
                }
            }

            namespace Microsoft.Extensions.Hosting
            {
                public interface IHostEnvironment
                {
                    string EnvironmentName { get; }
                }

                public static class HostingEnvironmentExtensions
                {
                    public static bool IsProduction(this IHostEnvironment environment) => true;

                    public static bool IsDevelopment(this IHostEnvironment environment) => false;
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
            [new LocalDevelopmentSigningProductionAnalyzer()]);

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

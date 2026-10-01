using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AsiBackbone.Analyzers.Tests;

/// <summary>
/// Tests for the <see cref="SignatureInputRequestAnalyzer"/> analyzer, which reports signing and verification requests
/// created without an explicit signature input.
/// </summary>
public sealed class SignatureInputRequestAnalyzerTests
{
    /// <summary>
    /// Tests that a signing request created without a signature input reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithoutSignatureInputReportsASIB004()
    {
        string source = SourceWithBody("_ = new SigningRequest(\"hash\");");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("SigningRequest", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that a target-typed signing request created without a signature input reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task TargetTypedSigningRequestWithoutSignatureInputReportsASIB004()
    {
        string source = SourceWithBody("SigningRequest request = new(\"hash\");");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that a signing request that sets its signature input does not report the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithSignatureInputDoesNotReport()
    {
        string source = SourceWithBody("_ = new SigningRequest(\"hash\") { SignatureInput = input };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests that assigning the default signature input still reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithDefaultSignatureInputReportsASIB004()
    {
        string source = SourceWithBody("_ = new SigningRequest(\"hash\") { SignatureInput = default };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that assigning <see cref="ReadOnlyMemory{T}.Empty"/> still reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithEmptySignatureInputReportsASIB004()
    {
        string source = SourceWithBody(
            "_ = new SigningRequest(\"hash\") { SignatureInput = ReadOnlyMemory<byte>.Empty };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that constructing an empty <see cref="ReadOnlyMemory{T}"/> still reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithConstructedEmptySignatureInputReportsASIB004()
    {
        string source = SourceWithBody(
            "_ = new SigningRequest(\"hash\") { SignatureInput = new ReadOnlyMemory<byte>() };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that assigning <see cref="Memory{T}.Empty"/> still reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithEmptyMemorySignatureInputReportsASIB004()
    {
        string source = SourceWithBody(
            "_ = new SigningRequest(\"hash\") { SignatureInput = Memory<byte>.Empty };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that constructing an empty <see cref="Memory{T}"/> still reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithConstructedEmptyMemorySignatureInputReportsASIB004()
    {
        string source = SourceWithBody(
            "_ = new SigningRequest(\"hash\") { SignatureInput = new Memory<byte>() };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that assigning <c>Array.Empty&lt;byte&gt;()</c> still reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithArrayEmptySignatureInputReportsASIB004()
    {
        string source = SourceWithBody(
            "_ = new SigningRequest(\"hash\") { SignatureInput = Array.Empty<byte>() };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that assigning a zero-length byte array still reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SigningRequestWithZeroLengthArraySignatureInputReportsASIB004()
    {
        string source = SourceWithBody(
            "_ = new SigningRequest(\"hash\") { SignatureInput = new byte[0] };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that a verification request created without a signature input reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task VerificationRequestWithoutSignatureInputReportsASIB004()
    {
        string source = SourceWithBody("_ = new SignatureVerificationRequest(\"hash\");");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("SignatureVerificationRequest", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that a verification request that sets its signature input does not report the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task VerificationRequestWithSignatureInputDoesNotReport()
    {
        string source = SourceWithBody("_ = new SignatureVerificationRequest(\"hash\") { SignatureInput = input };");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Tests that a managed-key sign request created without a signature input reports the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ManagedKeySignRequestWithoutSignatureInputReportsASIB004()
    {
        string source = SourceWithBody("_ = new ManagedKeySignRequest(\"hash\");");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SignatureInputRequestAnalyzer.DiagnosticId, diagnostic.Id);
    }

    /// <summary>
    /// Tests that an unrelated type with a <c>SignatureInput</c> property does not report the ASIB004 diagnostic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task UnrelatedTypeDoesNotReport()
    {
        string source = SourceWithBody("_ = new Other.SigningRequest(\"hash\");");

        ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    private static string SourceWithBody(string body)
    {
        return $$"""
            using System;
            using AsiBackbone.Core.Signing;
            using AsiBackbone.Signing.ManagedKey;

            public static class Sample
            {
                public static void Execute(ReadOnlyMemory<byte> input)
                {
                    {{body}}
                }
            }

            namespace AsiBackbone.Core.Signing
            {
                public sealed class SigningRequest
                {
                    public SigningRequest(string signingHash)
                    {
                    }

                    public ReadOnlyMemory<byte> SignatureInput { get; init; }
                }

                public sealed class SignatureVerificationRequest
                {
                    public SignatureVerificationRequest(string signingHash)
                    {
                    }

                    public ReadOnlyMemory<byte> SignatureInput { get; init; }
                }
            }

            namespace AsiBackbone.Signing.ManagedKey
            {
                public sealed class ManagedKeySignRequest
                {
                    public ManagedKeySignRequest(string signingHash)
                    {
                    }

                    public ReadOnlyMemory<byte> SignatureInput { get; init; }
                }
            }

            namespace Other
            {
                public sealed class SigningRequest
                {
                    public SigningRequest(string signingHash)
                    {
                    }

                    public ReadOnlyMemory<byte> SignatureInput { get; init; }
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
            [new SignatureInputRequestAnalyzer()]);

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

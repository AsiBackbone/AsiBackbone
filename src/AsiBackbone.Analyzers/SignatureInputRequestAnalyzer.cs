using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AsiBackbone.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SignatureInputRequestAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ASIB004";

    private const string SignatureInputPropertyName = "SignatureInput";

    /// <remarks>
    /// Each of these request types falls back to the pre-6.0 hash-only signature input when <c>SignatureInput</c> is not
    /// set. The fallback is internal, so it never surfaces the <c>ASIB902</c> deprecation warning on its own.
    /// </remarks>
    private static readonly string[] RequestTypeNames =
    [
        "AsiBackbone.Core.Signing.SigningRequest",
        "AsiBackbone.Core.Signing.SignatureVerificationRequest",
        "AsiBackbone.Signing.ManagedKey.ManagedKeySignRequest",
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Set SignatureInput on signing and verification requests",
        "'{0}' is created without setting SignatureInput, so it falls back to the pre-6.0 hash-only signature input; set SignatureInput, for example from GovernanceSignatureInput.CreateV1",
        "AsiBackbone.GovernanceSafety",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A signing or verification request without an explicit SignatureInput signs or verifies the UTF-8 text of the signing hash alone. That pre-6.0 input does not authenticate the canonical descriptors or the signing policy version and hash, and a signature produced from it fails default version 1 verification. GovernanceArtifactSigner and GovernanceArtifactVerifier set the input automatically.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;

        if (objectCreation.Type is not INamedTypeSymbol requestType || !IsRequestType(requestType))
        {
            return;
        }

        if (InitializesSignatureInput(objectCreation.Initializer))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                objectCreation.Syntax.GetLocation(),
                requestType.Name));
    }

    private static bool IsRequestType(INamedTypeSymbol type)
    {
        string typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        foreach (string requestTypeName in RequestTypeNames)
        {
            if (typeName.Equals(requestTypeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InitializesSignatureInput(IObjectOrCollectionInitializerOperation? initializer)
    {
        if (initializer is null)
        {
            return false;
        }

        foreach (IOperation memberInitializer in initializer.Initializers)
        {
            if (memberInitializer is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation propertyReference }
                && propertyReference.Property.Name.Equals(SignatureInputPropertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

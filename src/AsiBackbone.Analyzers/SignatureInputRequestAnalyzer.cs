using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AsiBackbone.Analyzers;

/// <summary>
/// Reports signing and verification request construction that omits an explicit signature input.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SignatureInputRequestAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the diagnostic identifier reported when a supported request omits
    /// an explicit signature input.
    /// </summary>
    public const string DiagnosticId = "ASIB004";

    /// <summary>
    /// Gets the name of the property that must be set to avoid falling back to the pre-6.0 hash-only signature input.
    /// </summary>
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

    /// <summary>
    /// Gets the diagnostics supported by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>
    /// Initializes the analyzer and registers operation analysis.
    /// </summary>
    /// <param name="context">The analysis context used to register analyzer actions.</param>
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
            if (memberInitializer is ISimpleAssignmentOperation
                {
                    Target: IPropertyReferenceOperation propertyReference
                } assignment
                && propertyReference.Property.Name.Equals(SignatureInputPropertyName, StringComparison.Ordinal)
                && !IsStaticallyEmptySignatureInput(assignment.Value))
            {
                return true;
            }
        }

        return false;
    }
    private static bool IsStaticallyEmptySignatureInput(IOperation operation)
    {
        operation = Unwrap(operation);

        return operation is IDefaultValueOperation || (operation is IPropertyReferenceOperation propertyReference
            && propertyReference.Property.Name.Equals("Empty", StringComparison.Ordinal)
            && IsMemoryLike(propertyReference.Property.ContainingType)) || (operation is IObjectCreationOperation objectCreation
            && objectCreation.Arguments.Length == 0
            && IsMemoryLike(objectCreation.Type)) || IsArrayEmptyInvocation(operation) || IsStaticallyEmptyByteArray(operation);
    }

    private static bool IsArrayEmptyInvocation(IOperation operation)
    {
        return operation is IInvocationOperation invocation
            && invocation.TargetMethod.Name.Equals("Empty", StringComparison.Ordinal)
            && invocation.TargetMethod.ContainingType.SpecialType == SpecialType.System_Array
            && invocation.TargetMethod.TypeArguments.Length == 1
            && invocation.TargetMethod.TypeArguments[0].SpecialType == SpecialType.System_Byte;
    }

    private static bool IsStaticallyEmptyByteArray(IOperation operation)
    {
        return operation is IArrayCreationOperation arrayCreation
            && arrayCreation.Type is IArrayTypeSymbol arrayType
            && arrayType.ElementType.SpecialType == SpecialType.System_Byte && (arrayCreation.Initializer is { ElementValues.Length: 0 } || (arrayCreation.DimensionSizes.Length == 1
            && arrayCreation.DimensionSizes[0].ConstantValue is { HasValue: true, Value: 0 }));
    }

    private static bool IsMemoryLike(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType
            && namedType.Arity == 1
            && (namedType.Name.Equals("Memory", StringComparison.Ordinal)
                || namedType.Name.Equals("ReadOnlyMemory", StringComparison.Ordinal))
            && namedType.ContainingNamespace?.ToDisplayString().Equals("System", StringComparison.Ordinal) == true;
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

}

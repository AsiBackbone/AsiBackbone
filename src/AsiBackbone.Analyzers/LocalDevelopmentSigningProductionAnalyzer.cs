using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AsiBackbone.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LocalDevelopmentSigningProductionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ASIB002";

    /// <summary>
    /// Reports local-development signing wired with no environment guard at all.
    /// </summary>
    public const string UnguardedDiagnosticId = "ASIB003";

    private const string LocalDevelopmentNamespace = "AsiBackbone.Signing.LocalDevelopment";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Do not wire local-development signing in production branches",
        "Local-development signing type '{0}' is used inside a production environment branch; use a host-owned production key provider instead",
        "AsiBackbone.ProductionSafety",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "LocalDevelopment signing providers generate in-process keys for tests, samples, and local proof paths only. They should not be registered or instantiated on a production configuration path.");

    /// <remarks>
    /// <see cref="Rule" /> only fires on a call the analyzer can see inside a production branch, so an unconditional
    /// registration — the shape that actually reaches production — was never reported. The runtime guard in
    /// <c>UseLocalDevelopmentSigning</c> throws in Production; this rule surfaces the same problem at build time.
    /// </remarks>
    private static readonly DiagnosticDescriptor UnguardedRule = new(
        UnguardedDiagnosticId,
        "Guard local-development signing by environment",
        "Local-development signing type '{0}' is wired without an environment guard; it will be registered in whichever environment this code runs in",
        "AsiBackbone.ProductionSafety",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "LocalDevelopment signing providers generate an in-process key that is never persisted, so artifacts they sign stop verifying after a restart. Registration should sit behind an environment check, or set LocalDevelopmentSigningOptions.AllowInProduction to state the intent explicitly.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, UnguardedRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (IsSuppressedByHostMarker(context.ContainingSymbol)
            || IsNestedInsideInvocationThatAlreadyReferencesLocalDevelopment(invocation))
        {
            return;
        }

        bool insideProductionBranch = IsInsideProductionBranch(invocation);

        if (!insideProductionBranch && IsInsideEnvironmentConditional(invocation))
        {
            return;
        }

        ITypeSymbol? localDevelopmentType = FindReferencedLocalDevelopmentType(invocation);
        if (localDevelopmentType is null)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                insideProductionBranch ? Rule : UnguardedRule,
                invocation.Syntax.GetLocation(),
                localDevelopmentType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;

        if (IsSuppressedByHostMarker(context.ContainingSymbol) || !IsInsideProductionBranch(objectCreation))
        {
            return;
        }

        ITypeSymbol? localDevelopmentType = FindLocalDevelopmentType(objectCreation.Type);
        if (localDevelopmentType is null)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                objectCreation.Syntax.GetLocation(),
                localDevelopmentType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    private static bool IsNestedInsideInvocationThatAlreadyReferencesLocalDevelopment(IInvocationOperation invocation)
    {
        return invocation.Parent is IArgumentOperation { Parent: IInvocationOperation parentInvocation }
            && FindReferencedLocalDevelopmentType(parentInvocation) is not null;
    }

    private static ITypeSymbol? FindReferencedLocalDevelopmentType(IInvocationOperation invocation)
    {
        ITypeSymbol? containingType = FindLocalDevelopmentType(invocation.TargetMethod.ContainingType);
        if (containingType is not null)
        {
            return containingType;
        }

        foreach (ITypeSymbol typeArgument in invocation.TargetMethod.TypeArguments)
        {
            ITypeSymbol? localDevelopmentType = FindLocalDevelopmentType(typeArgument);
            if (localDevelopmentType is not null)
            {
                return localDevelopmentType;
            }
        }

        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            ITypeSymbol? localDevelopmentType = FindLocalDevelopmentType(argument.Value.Type);
            if (localDevelopmentType is not null)
            {
                return localDevelopmentType;
            }
        }

        return FindLocalDevelopmentType(invocation.Type);
    }

    private static ITypeSymbol? FindLocalDevelopmentType(ITypeSymbol? type)
    {
        if (type is null)
        {
            return null;
        }

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (ITypeSymbol typeArgument in namedType.TypeArguments)
            {
                ITypeSymbol? localDevelopmentType = FindLocalDevelopmentType(typeArgument);
                if (localDevelopmentType is not null)
                {
                    return localDevelopmentType;
                }
            }
        }

        string namespaceName = type.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty;
        return namespaceName.Equals(LocalDevelopmentNamespace, StringComparison.Ordinal)
            ? type
            : null;
    }

    private static bool IsInsideProductionBranch(IOperation operation)
    {
        for (IOperation? current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IConditionalOperation conditionalOperation
                && IsProductionLikeCondition(conditionalOperation.Condition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the operation sits inside any conditional that tests the host environment.
    /// </summary>
    /// <remarks>
    /// A registration guarded by an environment check — for either the production or the non-production side — is a
    /// deliberate choice the host has already made. Only registrations with no environment check at all are reported by
    /// <see cref="UnguardedRule" />.
    /// </remarks>
    private static bool IsInsideEnvironmentConditional(IOperation operation)
    {
        for (IOperation? current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IConditionalOperation conditionalOperation
                && ConditionReferencesEnvironment(conditionalOperation.Condition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConditionReferencesEnvironment(IOperation operation)
    {
        operation = Unwrap(operation);

        if (IsEnvironmentNameReference(operation))
        {
            return true;
        }

        switch (operation)
        {
            case IInvocationOperation invocationOperation:
                IMethodSymbol method = invocationOperation.TargetMethod.ReducedFrom ?? invocationOperation.TargetMethod;
                string namespaceName = method.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty;

                return namespaceName.Equals("Microsoft.Extensions.Hosting", StringComparison.Ordinal)
                    || InvocationComparesEnvironmentNameToProduction(invocationOperation)
                    || IsEnvironmentNameReference(invocationOperation.Instance);

            case IBinaryOperation binaryOperation:
                return ConditionReferencesEnvironment(binaryOperation.LeftOperand)
                    || ConditionReferencesEnvironment(binaryOperation.RightOperand);

            case IUnaryOperation unaryOperation:
                return ConditionReferencesEnvironment(unaryOperation.Operand);

            case IPropertyReferenceOperation propertyReference:
                return propertyReference.Property.Name.Contains("Environment", StringComparison.Ordinal);

            default:
                return false;
        }
    }

    private static bool IsProductionLikeCondition(IOperation operation)
    {
        operation = Unwrap(operation);

        return operation switch
        {
            IInvocationOperation invocationOperation => IsProductionInvocation(invocationOperation)
                || InvocationComparesEnvironmentNameToProduction(invocationOperation),
            IBinaryOperation binaryOperation => BinaryComparesEnvironmentNameToProduction(binaryOperation),
            _ => false
        };
    }

    private static bool IsProductionInvocation(IInvocationOperation invocation)
    {
        IMethodSymbol method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        string namespaceName = method.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty;

        return method.Name.Equals("IsProduction", StringComparison.Ordinal)
            && namespaceName.Equals("Microsoft.Extensions.Hosting", StringComparison.Ordinal);
    }

    private static bool InvocationComparesEnvironmentNameToProduction(IInvocationOperation invocation)
    {
        if (!invocation.TargetMethod.Name.Equals("Equals", StringComparison.Ordinal))
        {
            return false;
        }

        bool hasEnvironmentName = IsEnvironmentNameReference(invocation.Instance);
        bool hasProductionConstant = false;

        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            IOperation value = Unwrap(argument.Value);
            hasEnvironmentName = hasEnvironmentName || IsEnvironmentNameReference(value);
            hasProductionConstant = hasProductionConstant || IsProductionConstant(value);
        }

        return hasEnvironmentName && hasProductionConstant;
    }

    private static bool BinaryComparesEnvironmentNameToProduction(IBinaryOperation binaryOperation)
    {
        if (binaryOperation.OperatorKind != BinaryOperatorKind.Equals)
        {
            return false;
        }

        IOperation left = Unwrap(binaryOperation.LeftOperand);
        IOperation right = Unwrap(binaryOperation.RightOperand);

        return (IsEnvironmentNameReference(left) && IsProductionConstant(right))
            || (IsEnvironmentNameReference(right) && IsProductionConstant(left));
    }

    private static bool IsEnvironmentNameReference(IOperation? operation)
    {
        operation = operation is null ? null : Unwrap(operation);

        return operation is IPropertyReferenceOperation propertyReference
            && propertyReference.Property.Name.Equals("EnvironmentName", StringComparison.Ordinal);
    }

    private static bool IsProductionConstant(IOperation operation)
    {
        return operation.ConstantValue.HasValue
            && operation.ConstantValue.Value is string value
            && value.Equals("Production", StringComparison.OrdinalIgnoreCase);
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversionOperation)
        {
            operation = conversionOperation.Operand;
        }

        return operation;
    }

    private static bool IsSuppressedByHostMarker(ISymbol? symbol)
    {
        for (ISymbol? current = symbol; current is not null; current = current.ContainingSymbol)
        {
            foreach (AttributeData attribute in current.GetAttributes())
            {
                string? attributeName = attribute.AttributeClass?.Name;
                if (attributeName is "AsiBackboneProductionConfigurationReviewed" or "AsiBackboneProductionConfigurationReviewedAttribute")
                {
                    return true;
                }
            }

            if (current is INamedTypeSymbol)
            {
                break;
            }
        }

        return false;
    }
}

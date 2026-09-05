using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AsiBackbone.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GovernanceArtifactPersistenceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ASIB001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Persist or continue AsiBackbone governance artifact",
        "AsiBackbone governance artifact '{0}' is created or returned and then discarded; persist audit/outbox residue or pass it to a safe continuation before execution",
        "AsiBackbone.GovernanceSafety",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Governance decisions, audit residue, capability grants, handshake outcomes, and outbox artifacts should not be created and discarded without persistence, outbox emission, audit recording, or a deliberate host-owned continuation path.");

    private static readonly ImmutableHashSet<string> GovernanceArtifactTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "AsiBackbone.Core.Audit.AuditLedgerRecord",
        "AsiBackbone.Core.Audit.AuditResidue",
        "AsiBackbone.Core.CapabilityTokens.CapabilityGrantUseResult",
        "AsiBackbone.Core.CapabilityTokens.CapabilityGrantValidationResult",
        "AsiBackbone.Core.CapabilityTokens.CapabilityTokenGrant",
        "AsiBackbone.Core.Decisions.GovernanceDecision",
        "AsiBackbone.Core.Emissions.GovernanceEmissionEnvelope",
        "AsiBackbone.Core.Emissions.GovernanceEmissionResult",
        "AsiBackbone.Core.Handshakes.LiabilityHandshakeAcknowledgment",
        "AsiBackbone.Core.Outbox.GovernanceOutboxEntry");

    private static readonly ImmutableHashSet<string> PersistenceBoundaryTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "AsiBackbone.Core.Audit.IAsiBackboneAuditLedgerStore",
        "AsiBackbone.Core.Audit.IAsiBackboneAuditResidueLifecycleStore",
        "AsiBackbone.Core.Audit.IAsiBackboneAuditSink",
        "AsiBackbone.Core.CapabilityTokens.ICapabilityGrantUseStore",
        "AsiBackbone.Core.Outbox.IAsiBackboneGovernanceOutboxStore");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeExpressionStatement, OperationKind.ExpressionStatement);
        context.RegisterOperationAction(AnalyzeDiscardAssignment, OperationKind.SimpleAssignment);
    }

    private static void AnalyzeExpressionStatement(OperationAnalysisContext context)
    {
        var expressionStatement = (IExpressionStatementOperation)context.Operation;

        if (IsSuppressedByHostMarker(context.ContainingSymbol) || expressionStatement.Operation is ISimpleAssignmentOperation)
        {
            return;
        }

        IOperation expression = UnwrapAwait(expressionStatement.Operation);
        ITypeSymbol? artifactType = GetGovernanceArtifactType(expression.Type);
        if (artifactType is null || !IsArtifactProducerOperation(expression))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                expression.Syntax.GetLocation(),
                artifactType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    private static void AnalyzeDiscardAssignment(OperationAnalysisContext context)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;

        if (IsSuppressedByHostMarker(context.ContainingSymbol)
            || (assignment.Target is not IDiscardOperation && !IsKnownAsyncWrapper(assignment.Value.Type)))
        {
            return;
        }

        IOperation value = UnwrapAwait(assignment.Value);
        ITypeSymbol? artifactType = GetGovernanceArtifactType(value.Type);
        if (artifactType is null || !IsArtifactProducerOperation(value))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                assignment.Syntax.GetLocation(),
                artifactType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    private static IOperation UnwrapAwait(IOperation operation)
    {
        return operation is IAwaitOperation awaitOperation
            ? awaitOperation.Operation
            : operation;
    }

    private static bool IsArtifactProducerOperation(IOperation? operation)
    {
        if (operation is null)
        {
            return false;
        }

        operation = UnwrapAwait(operation);

        while (operation is IConversionOperation conversionOperation)
        {
            operation = conversionOperation.Operand;
        }

        return operation switch
        {
            IInvocationOperation invocationOperation => !IsPersistenceBoundaryInvocation(invocationOperation),
            IObjectCreationOperation => true,
            IPropertyReferenceOperation => true,
            IConditionalOperation conditionalOperation => IsArtifactProducerOperation(conditionalOperation.WhenTrue)
                || IsArtifactProducerOperation(conditionalOperation.WhenFalse),
            ICoalesceOperation coalesceOperation => IsArtifactProducerOperation(coalesceOperation.Value)
                || IsArtifactProducerOperation(coalesceOperation.WhenNull),
            _ => false
        };
    }

    private static bool IsPersistenceBoundaryInvocation(IInvocationOperation invocation)
    {
        return invocation.Instance?.Type is INamedTypeSymbol receiverType
            && (IsPersistenceBoundaryType(receiverType)
                || receiverType.AllInterfaces.Any(IsPersistenceBoundaryType));
    }

    private static bool IsPersistenceBoundaryType(INamedTypeSymbol type)
    {
        string typeName = type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return PersistenceBoundaryTypeNames.Contains(typeName);
    }

    private static ITypeSymbol? GetGovernanceArtifactType(ITypeSymbol? type)
    {
        ITypeSymbol? candidate = UnwrapKnownWrapper(type);
        if (candidate is null)
        {
            return null;
        }

        string candidateName = candidate.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return GovernanceArtifactTypeNames.Contains(candidateName) ? candidate : null;
    }

    private static ITypeSymbol? UnwrapKnownWrapper(ITypeSymbol? type)
    {
        while (type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } namedType)
        {
            string namespaceName = namedType.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty;

            if ((namespaceName == "System.Threading.Tasks" && (namedType.Name == "Task" || namedType.Name == "ValueTask"))
                || (namespaceName == "AsiBackbone.Core.Results" && namedType.Name == "OperationResult"))
            {
                type = namedType.TypeArguments[0];
                continue;
            }

            break;
        }

        return type;
    }

    private static bool IsKnownAsyncWrapper(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } namedType)
        {
            return false;
        }

        string namespaceName = namedType.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty;
        return namespaceName == "System.Threading.Tasks" && (namedType.Name == "Task" || namedType.Name == "ValueTask");
    }

    private static bool IsSuppressedByHostMarker(ISymbol? symbol)
    {
        for (ISymbol? current = symbol; current is not null; current = current.ContainingSymbol)
        {
            foreach (AttributeData attribute in current.GetAttributes())
            {
                string? attributeName = attribute.AttributeClass?.Name;
                if (attributeName is "AsiBackbonePersistenceHandled" or "AsiBackbonePersistenceHandledAttribute")
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

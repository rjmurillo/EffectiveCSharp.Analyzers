namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #9 - Minimize boxing and unboxing.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MinimizeBoxingUnboxingAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.MinimizeBoxingUnboxing;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Minimize boxing and unboxing",
        messageFormat: "Consider using an alternative implementation to avoid boxing and unboxing",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(
            AnalyzeOperation,
            OperationKind.Conversion,
            OperationKind.SimpleAssignment,
            OperationKind.Argument,
            OperationKind.Return,
            OperationKind.Invocation,
            OperationKind.ArrayElementReference);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        switch (context.Operation)
        {
            case IConversionOperation conversionOperation:
                AnalyzeConversionOperation(conversionOperation, context);
                break;

            case ISimpleAssignmentOperation simpleAssignmentOperation:
                AnalyzeSimpleAssignmentOperation(simpleAssignmentOperation, context);
                break;

            case IArgumentOperation argumentOperation:
                AnalyzeArgumentOperation(argumentOperation, context);
                break;

            case IReturnOperation returnOperation:
                AnalyzeReturnOperation(returnOperation, context);
                break;

            case IInvocationOperation invocationOperation:
                AnalyzeInvocationOperation(invocationOperation, context);
                break;

            case IArrayElementReferenceOperation arrayElementReferenceOperation:
                AnalyzeArrayElementReferenceOperation(arrayElementReferenceOperation, context);
                break;

            default:
                throw new NotSupportedException($"Unsupported operation kind: {context.Operation.Kind}");
        }
    }

    private static void AnalyzeArrayElementReferenceOperation(IArrayElementReferenceOperation arrayElementReferenceOperation, OperationAnalysisContext context)
    {
        if (arrayElementReferenceOperation.ArrayReference.Type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Collections_Generic_IList_T }
            && arrayElementReferenceOperation.ArrayReference is ILocalReferenceOperation { Type: INamedTypeSymbol listTypeSymbol }
            && listTypeSymbol.TypeArguments[0].IsValueType)
        {
            Diagnostic diagnostic = arrayElementReferenceOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeInvocationOperation(IInvocationOperation invocationOperation, OperationAnalysisContext context)
    {
        if (invocationOperation.IsInvocationToBoxedType())
        {
            Diagnostic diagnostic = invocationOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeConversionOperation(IConversionOperation conversionOperation, OperationAnalysisContext context)
    {
        if (conversionOperation.IsBoxingOperation())
        {
            Diagnostic diagnostic = conversionOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeSimpleAssignmentOperation(ISimpleAssignmentOperation simpleAssignmentOperation, OperationAnalysisContext context)
    {
        if (simpleAssignmentOperation.Value.IsBoxingOperation()
            || simpleAssignmentOperation.IsAssignmentToBoxedType())
        {
            Diagnostic diagnostic = simpleAssignmentOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeArgumentOperation(IArgumentOperation argumentOperation, OperationAnalysisContext context)
    {
        if (argumentOperation.Value.IsBoxingOperation()
            || argumentOperation.IsArgumentToBoxedType())
        {
            Diagnostic diagnostic = argumentOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeReturnOperation(IReturnOperation returnOperation, OperationAnalysisContext context)
    {
        if (returnOperation.ReturnedValue.IsBoxingOperation()
            || returnOperation.IsReturnToBoxedType())
        {
            Diagnostic diagnostic = returnOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }
}

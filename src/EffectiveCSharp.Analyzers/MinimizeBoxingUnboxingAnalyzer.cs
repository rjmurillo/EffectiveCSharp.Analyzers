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
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Conversion);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ElementAccessExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        ElementAccessExpressionSyntax elementAccess = (ElementAccessExpressionSyntax)context.Node;

        // Get the type of the accessed object
        TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(elementAccess.Expression, context.CancellationToken);

        if (typeInfo.Type is not INamedTypeSymbol { IsGenericType: true } namedType)
        {
            return;
        }

        string constructedTypeName = namedType.ConstructedFrom.ToString();
        ITypeSymbol? elementType = null;

        // Check if it's a List<T>
        if (string.Equals(constructedTypeName, "System.Collections.Generic.List<T>", StringComparison.Ordinal))
        {
            elementType = namedType.TypeArguments[0];
        }

        // Check if the element type is a value type
        if (elementType is { IsValueType: true })
        {
            // Create and report a diagnostic if the element is accessed directly
            Diagnostic diagnostic = elementAccess.GetLocation().CreateDiagnostic(Rule, elementType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        switch (context.Operation)
        {
            case IConversionOperation conversionOperation:
                AnalyzeConversionOperation(conversionOperation, context);
                break;

            default:
                throw new NotSupportedException($"Unsupported operation kind: {context.Operation.Kind}");
        }
    }

    private static void AnalyzeConversionOperation(IConversionOperation conversionOperation, OperationAnalysisContext context)
    {
        if (conversionOperation.IsBoxingOrUnboxingOperation())
        {
            Diagnostic diagnostic = conversionOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }
}

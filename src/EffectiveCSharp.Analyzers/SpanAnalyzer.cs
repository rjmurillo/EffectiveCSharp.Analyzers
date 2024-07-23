namespace EffectiveCSharp.Analyzers;

/// <summary>
/// Analyzer that suggests using <see cref="Span{T}"/> instead of arrays for better performance.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SpanAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.UseSpanInstead;
    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Use Span<T> for performance",
        messageFormat: "Consider using Span<T> instead of array for better performance",
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
        context.RegisterSyntaxNodeAction(AnalyzeArrayCreation, SyntaxKind.ArrayCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeArrayAccess, SyntaxKind.ElementAccessExpression);
    }

    private static void AnalyzeArrayCreation(SyntaxNodeAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ArrayCreationExpressionSyntax arrayCreation = (ArrayCreationExpressionSyntax)context.Node;

        if (IsInsideSpanInitialization(arrayCreation))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(arrayCreation.Type, context.CancellationToken).Type is IArrayTypeSymbol)
        {
            Diagnostic diagnostic = arrayCreation.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsInsideSpanInitialization(ArrayCreationExpressionSyntax arrayCreation)
    {
        // Check if the parent is a Span<T> or ReadOnlySpan<T> creation
        // example: new Span<int>(new int[10]);
        if (arrayCreation.Parent?.Parent?.Parent is not ObjectCreationExpressionSyntax objectCreation)
        {
            return false;
        }

        if (objectCreation.Type is not GenericNameSyntax type)
        {
            return false;
        }

        string typeName = type.Identifier.Text;
        return typeName is WellKnownTypes.Span or WellKnownTypes.ReadOnlySpan;
    }

    private static void AnalyzeArrayAccess(SyntaxNodeAnalysisContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        ElementAccessExpressionSyntax elementAccess = (ElementAccessExpressionSyntax)context.Node;

        if (context.SemanticModel.GetTypeInfo(elementAccess.Expression, context.CancellationToken).Type is IArrayTypeSymbol)
        {
            Diagnostic diagnostic = elementAccess.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }
}

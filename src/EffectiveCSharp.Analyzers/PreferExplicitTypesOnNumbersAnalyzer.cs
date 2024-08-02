namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #1 - Prefer implicit types except on numbers.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferExplicitTypesOnNumbersAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.PreferImplicitlyTypedLocalVariables;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Prefer implicitly typed local variables",
        messageFormat: "Use explicit type instead of 'var' for numeric variables",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Use var to declare local variables for better readability and efficiency, except for built-in numeric types where explicit typing prevents potential conversion issues.",
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.LocalDeclarationStatement);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        LocalDeclarationStatementSyntax localDeclaration = (LocalDeclarationStatementSyntax)context.Node;

        foreach (VariableDeclaratorSyntax variable in localDeclaration.Declaration.Variables)
        {
            ExpressionSyntax? initializer = variable.Initializer?.Value;

            if (initializer is null)
            {
                continue;
            }

            TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(initializer, context.CancellationToken);
            ITypeSymbol? type = typeInfo.ConvertedType;

            if (type?.IsNumericType() != true)
            {
                continue;
            }

            if (localDeclaration.Declaration.Type.IsVar)
            {
                Diagnostic diagnostic = localDeclaration.GetLocation().CreateDiagnostic(Rule);
                context.ReportDiagnostic(diagnostic);
            }
            else if (HasPotentialConversionIssues(type, initializer, context.SemanticModel, context.CancellationToken))
            {
                Diagnostic diagnostic = initializer.GetLocation().CreateDiagnostic(Rule);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool HasPotentialConversionIssues(ITypeSymbol type, ExpressionSyntax initializer, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        TypeInfo typeInfo = semanticModel.GetTypeInfo(initializer, cancellationToken);
        return !SymbolEqualityComparer.IncludeNullability.Equals(typeInfo.Type, type);
    }
}

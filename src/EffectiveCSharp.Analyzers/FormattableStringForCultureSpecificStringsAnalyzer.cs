namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #5 - Use FormattableString for culture specific strings
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FormattableStringForCultureSpecificStringsAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = "Prefer FormattableString for culture-specific strings";
    private static readonly LocalizableString MessageFormat = "Use 'FormattableString' instead of 'string' for culture-specific interpolated strings";
    private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticIds.PreferFormattableStringForCultureSpecificStrings,
            Title,
            MessageFormat,
            Categories.Globalization,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{DiagnosticIds.PreferFormattableStringForCultureSpecificStrings}.md");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            CSharpParseOptions? parseOptions = compilationContext.Compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
            if (parseOptions != null && parseOptions.LanguageVersion < LanguageVersion.CSharp10)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InterpolatedStringExpression);
        });
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        InterpolatedStringExpressionSyntax interpolatedString = (InterpolatedStringExpressionSyntax)context.Node;
        SyntaxNode? parent = interpolatedString.Parent;

        if (parent == null)
        {
            return;
        }

        ITypeSymbol? targetType = null;

        if (parent is AssignmentExpressionSyntax assignment)
        {
            // Handling direct assignment in method scope
            targetType = context.SemanticModel.GetTypeInfo(assignment.Left, context.CancellationToken).Type;
        }
        else if (parent is EqualsValueClauseSyntax equalsValueClause)
        {
            SyntaxNode? declaration = equalsValueClause.Parent;

            // Handling variable/field initialization
            if (declaration is VariableDeclaratorSyntax variableDeclarator)
            {
                ISymbol? symbol = context.SemanticModel.GetDeclaredSymbol(variableDeclarator, context.CancellationToken);
                targetType = (symbol as ILocalSymbol)?.Type ?? (symbol as IFieldSymbol)?.Type;
            }
            // Handling property initialization
            else if (declaration is PropertyDeclarationSyntax propertyDeclaration)
            {
                targetType = context.SemanticModel.GetTypeInfo(propertyDeclaration.Type, context.CancellationToken).Type;
            }
        }

        // If the target type is string (and not FormattableString), report a diagnostic
        if (targetType?.SpecialType == SpecialType.System_String)
        {
            ReportDiagnostic(context, interpolatedString, "FormattableString", "string");
        }
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, InterpolatedStringExpressionSyntax interpolatedString, string suggestion, string original)
    {
        Diagnostic diagnostic = interpolatedString.GetLocation().CreateDiagnostic(Rule, suggestion, original);
        context.ReportDiagnostic(diagnostic);
    }
}

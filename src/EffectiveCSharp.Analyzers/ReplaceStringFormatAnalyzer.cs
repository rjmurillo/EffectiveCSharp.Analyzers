using System.Text.RegularExpressions;

namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #4 - Replace string.Format with interpolated string.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReplaceStringFormatAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = DiagnosticIds.ReplaceStringFormatWithInterpolatedString;
    private const string Title = "Replace string.Format with interpolated string";
    private const string MessageFormat = "Replace '{0}' with interpolated string";
    private const string Description = "Replace string.Format with interpolated string.";
    private const string Category = "Style";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers{ThisAssembly.GitCommitId}/docs/{DiagnosticId}.md");

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

            compilationContext.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InvocationExpression);
        });
    }

#pragma warning disable MA0051 // Method is too long
    private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        InvocationExpressionSyntax invocationExpr = (InvocationExpressionSyntax)context.Node;

        SymbolInfo info = context.SemanticModel.GetSymbolInfo(invocationExpr, context.CancellationToken);
        ISymbol? symbol = info.Symbol;

        if (symbol is not { Name: "Format" }
            || !symbol.IsStatic
            || !symbol.ContainingType.IsString())
        {
            return;
        }

        SeparatedSyntaxList<ArgumentSyntax> argumentList = invocationExpr.ArgumentList.Arguments;

        if (argumentList.Count < 2)
        {
            return;
        }

        if (argumentList[0].Expression is not LiteralExpressionSyntax formatArgument
            || !formatArgument.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        var formatString = formatArgument.Token.ValueText;
        if (!ContainsPlaceholders(formatString))
        {
            return;
        }

        Diagnostic diagnostic = invocationExpr.GetLocation().CreateDiagnostic(Rule, invocationExpr.ToString());
        context.ReportDiagnostic(diagnostic);
    }
#pragma warning restore MA0051 // Method is too long

    private static bool ContainsPlaceholders(string formatString)
    {
        Regex regex = new(@"\{.*?\}");
        return regex.IsMatch(formatString);
    }
}

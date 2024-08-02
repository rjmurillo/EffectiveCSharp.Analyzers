﻿namespace EffectiveCSharp.Analyzers;

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

        bool? containsVerbatimString = null;
        bool containsNormalString = false;
        bool? containsPlaceholders = null;

#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions
        foreach (ArgumentSyntax argument in invocationExpr.ArgumentList.Arguments)
        {
            ExpressionSyntax expression = argument.Expression;

            if (expression.IsKind(SyntaxKind.InterpolatedStringExpression))
            {
                return;
            }

            if (expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                string text = ((LiteralExpressionSyntax)expression).Token.Text;
                if (text.Contains("{") || text.Contains("}"))
                {
                    containsPlaceholders = true;
                }

                if (text.StartsWith("@", StringComparison.Ordinal))
                {
                    switch (containsVerbatimString)
                    {
                        case null:
                            containsVerbatimString = true;
                            break;
                        case false:
                            return;
                    }
                }
                else
                {
                    if (!containsVerbatimString.HasValue)
                    {
                        containsVerbatimString = false;
                    }
                    else if (containsVerbatimString.Value)
                    {
                        return;
                    }
                }
            }
            else
            {
                containsNormalString = true;
            }
        }
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions

#pragma warning disable S2589 // Boolean expressions should not be gratuitous
        if (((containsPlaceholders ?? false) || (containsVerbatimString ?? false))
            && !containsNormalString)
        {
            Diagnostic diagnostic = invocationExpr.GetLocation().CreateDiagnostic(Rule, invocationExpr.ToString());

            context.ReportDiagnostic(diagnostic);
        }
#pragma warning restore S2589 // Boolean expressions should not be gratuitous
    }
#pragma warning restore MA0051 // Method is too long
}

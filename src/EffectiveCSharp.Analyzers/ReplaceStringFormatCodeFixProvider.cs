using System.Text.RegularExpressions;

namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="ReplaceStringFormatAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReplaceStringFormatCodeFixProvider))]
[Shared]
public class ReplaceStringFormatCodeFixProvider : CodeFixProvider
{
    private const string Title = "Replace with interpolated string";

    /// <inheritdoc />
    public override sealed ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.ReplaceStringFormatWithInterpolatedString);

    /// <inheritdoc />
    public override sealed FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override sealed async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        InvocationExpressionSyntax? invocationExpr = root?.FindNode(diagnosticSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedSolution: c => ReplaceWithInterpolatedStringAsync(context.Document, invocationExpr, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private static bool NeedsParentheses(ExpressionSyntax expression)
    {
        // Check if the expression is complex and needs to be wrapped in parentheses
        return expression is BinaryExpressionSyntax or ConditionalExpressionSyntax or AssignmentExpressionSyntax or CastExpressionSyntax;
    }

    private static string CreateInterpolatedString(string formatString, ArgumentSyntax[] arguments)
    {
        string result = formatString;

        for (int i = 0; i < arguments.Length; i++)
        {
            string argumentText = arguments[i].ToString();

            // Wrap in parentheses if the argument is a complex expression
            if (NeedsParentheses(arguments[i].Expression))
            {
                argumentText = $"({argumentText})";
            }

            result = Regex.Replace(result, $@"\{{{i}(.*?)\}}", $"{{{argumentText}$1}}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        }

        return $"$\"{result}\"";
    }

    private static async Task<Solution> ReplaceWithInterpolatedStringAsync(Document document, InvocationExpressionSyntax? invocationExpr, CancellationToken cancellationToken)
    {
        if (invocationExpr == null)
        {
            return document.Project.Solution;
        }

        LiteralExpressionSyntax? formatStringLiteral = invocationExpr.ArgumentList.Arguments.First().Expression as LiteralExpressionSyntax;
        string? formatString = formatStringLiteral?.Token.ValueText;

        if (string.IsNullOrEmpty(formatString))
        {
            return document.Project.Solution;
        }

        ArgumentSyntax[] arguments = invocationExpr.ArgumentList.Arguments.Skip(1).ToArray();

        // Replace format placeholders with corresponding arguments in an interpolated string format
        string interpolatedString = CreateInterpolatedString(formatString!, arguments);
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode? newRoot = root?.ReplaceNode(invocationExpr, SyntaxFactory.ParseExpression(interpolatedString));

        return newRoot != null
            ? document.WithSyntaxRoot(newRoot).Project.Solution
            : document.Project.Solution;
    }
}

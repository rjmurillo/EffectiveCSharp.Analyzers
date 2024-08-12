namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="FormattableStringForCultureSpecificStringsAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormattableStringForCultureSpecificStringsCodeFixProvider))]
[Shared]
public class FormattableStringForCultureSpecificStringsCodeFixProvider : CodeFixProvider
{
    private static readonly string Title = "Use string.Create or FormattableString";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.PreferFormattableStringForCultureSpecificStrings);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the interpolated string expression identified by the diagnostic
        InterpolatedStringExpressionSyntax? interpolatedString = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<InterpolatedStringExpressionSyntax>().First();

        if (interpolatedString == null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedSolution: c => UseCultureSpecificStringAsync(context.Document, interpolatedString, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Solution> UseCultureSpecificStringAsync(Document document, InterpolatedStringExpressionSyntax interpolatedString, CancellationToken cancellationToken)
    {
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (semanticModel == null)
        {
            return document.Project.Solution;
        }

        Compilation compilation = semanticModel.Compilation;

        ExpressionSyntax newExpression = compilation.IsCSharpVersionOrLater(LanguageVersion.CSharp10)
            ? CreateStringCreateExpression(interpolatedString)
            : CreateFormattableStringExpression(interpolatedString);

        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root == null)
        {
            return document.Project.Solution;
        }

        SyntaxNode newRoot = root.ReplaceNode(interpolatedString, newExpression);

        return document.WithSyntaxRoot(newRoot).Project.Solution;
    }

    private static InvocationExpressionSyntax CreateStringCreateExpression(InterpolatedStringExpressionSyntax interpolatedString)
    {
        // Replace with string.Create(CultureInfo.CurrentCulture, ...) for .NET 6 and later
        ArgumentListSyntax arguments = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(
                new[]
                {
                    SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("CultureInfo"), SyntaxFactory.IdentifierName("CurrentCulture"))),
                    SyntaxFactory.Argument(interpolatedString),
                }));


        InvocationExpressionSyntax invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)), SyntaxFactory.IdentifierName("Create")),
            arguments);

        return invocation;
    }

    private static InvocationExpressionSyntax CreateFormattableStringExpression(InterpolatedStringExpressionSyntax interpolatedString)
    {
        // Replace with FormattableString.CurrentCulture(...) just to be explicit about what was happening before
        InvocationExpressionSyntax invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("FormattableString"), SyntaxFactory.IdentifierName("CurrentCulture")),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Argument(interpolatedString) })));

        return invocation;
    }
}

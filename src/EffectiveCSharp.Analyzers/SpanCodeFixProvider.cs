namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="SpanAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SpanCodeFixProvider))]
[Shared]
public class SpanCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use Span<T>";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.UseSpanInstead);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
        SyntaxNode? declaration = root?.FindNode(diagnosticSpan);

        switch (declaration)
        {
            case ArrayCreationExpressionSyntax arrayCreation:
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedSolution: c => UseSpanAsync(context.Document, arrayCreation, c),
                        equivalenceKey: Title),
                    diagnostic);
                break;
            case ElementAccessExpressionSyntax elementAccess:
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedSolution: c => UseSpanElementAccessAsync(context.Document, elementAccess, c),
                        equivalenceKey: Title),
                    diagnostic);
                break;
        }
    }

    private async Task<Solution> UseSpanAsync(Document document, ArrayCreationExpressionSyntax arrayCreation, CancellationToken cancellationToken)
    {
        string elementType = arrayCreation.Type.ElementType.ToString();
        InitializerExpressionSyntax? arrayInitializer = arrayCreation.Initializer;
        string spanText = $"new Span<{elementType}>({arrayInitializer})";

        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode? newRoot = root?.ReplaceNode(arrayCreation, SyntaxFactory.ParseExpression(spanText));

        return newRoot != null
            ? document.WithSyntaxRoot(newRoot).Project.Solution
            : document.Project.Solution;
    }

    private async Task<Solution> UseSpanElementAccessAsync(Document document, ElementAccessExpressionSyntax elementAccess, CancellationToken cancellationToken)
    {
        string spanExpression = elementAccess.Expression.ToString();
        string indexArgument = elementAccess.ArgumentList.Arguments.First().ToString();
        string spanAccessText = $"{spanExpression}.Slice({indexArgument}, 1)[0]";

        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode? newRoot = root?.ReplaceNode(elementAccess, SyntaxFactory.ParseExpression(spanAccessText));

        return newRoot != null
            ? document.WithSyntaxRoot(newRoot).Project.Solution
            : document.Project.Solution;
    }
}

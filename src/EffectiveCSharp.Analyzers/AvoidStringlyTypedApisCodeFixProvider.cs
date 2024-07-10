namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="AvoidStringlyTypedApisAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AvoidStringlyTypedApisCodeFixProvider))]
[Shared]
public class AvoidStringlyTypedApisCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use nameof operator";

    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.AvoidStringlyTypedApis);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics.First();
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        SyntaxNode? node = root?.FindNode(diagnosticSpan);

        LiteralExpressionSyntax? literalExpression = node switch
        {
            // Check if the node is a LiteralExpressionSyntax directly
            LiteralExpressionSyntax literalNode => literalNode,
            // Check if the node is an ArgumentSyntax containing a LiteralExpressionSyntax
            ArgumentSyntax { Expression: LiteralExpressionSyntax argLiteralNode } => argLiteralNode,
            _ => null
        };

        if (literalExpression != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedSolution: c => UseNameofOperatorAsync(context.Document, literalExpression, c),
                    equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Solution> UseNameofOperatorAsync(Document document, LiteralExpressionSyntax? literalExpression, CancellationToken cancellationToken)
    {
        string literalValue = literalExpression?.Token.ValueText;

        // Walk up the syntax tree to find the containing class or method
        TypeDeclarationSyntax? containingClass = literalExpression?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        MethodDeclarationSyntax? containingMethod = literalExpression?.FirstAncestorOrSelf<MethodDeclarationSyntax>();

        string? nameofExpressionText = null;

        if (containingClass != null)
        {
            SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (semanticModel.GetDeclaredSymbol(containingClass, cancellationToken) is { } containingTypeSymbol)
            {
                IEnumerable<string> memberNames = containingTypeSymbol.GetMembers().Select(member => member.Name);

                if (memberNames.Contains(literalValue))
                {
                    nameofExpressionText = $"nameof({literalValue})";
                }
            }
        }

        if (nameofExpressionText == null && containingMethod != null)
        {
            SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (semanticModel.GetDeclaredSymbol(containingMethod, cancellationToken) is { } methodSymbol)
            {
                IEnumerable<string> parameterNames = methodSymbol.Parameters.Select(parameter => parameter.Name);

                if (parameterNames.Contains(literalValue))
                {
                    nameofExpressionText = $"nameof({literalValue})";
                }
            }
        }

        if (nameofExpressionText == null)
        {
            return document.Project.Solution;
        }

        if (literalExpression != null)
        {
            ExpressionSyntax nameofExpression = SyntaxFactory.ParseExpression(nameofExpressionText)
                .WithTriviaFrom(literalExpression);

            SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode? newRoot = root?.ReplaceNode(literalExpression, nameofExpression);

            if (newRoot != null)
            {
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }
        }

        return document.Project.Solution;
    }
}

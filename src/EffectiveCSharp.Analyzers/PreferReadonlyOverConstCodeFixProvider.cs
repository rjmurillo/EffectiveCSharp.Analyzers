namespace EffectiveCSharp.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferReadonlyOverConstCodeFixProvider))]
[Shared]
public class PreferReadonlyOverConstCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use readonly instead of const";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.PreferReadonlyOverConst);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics.First();
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        if (root?.FindNode(diagnosticSpan) is FieldDeclarationSyntax declaration)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedSolution: c => ReplaceConstWithReadonlyAsync(context.Document, declaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Solution> ReplaceConstWithReadonlyAsync(Document document, FieldDeclarationSyntax constDeclaration, CancellationToken cancellationToken)
    {
        SyntaxTokenList modifiers = constDeclaration.Modifiers;

        // Find the const token within the modifiers
        SyntaxToken constToken = modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.ConstKeyword));

        if (constToken == default)
        {
            // If we did not find the const token, we should return the unchanged solution
            return document.Project.Solution;
        }

        // Create the readonly token
        SyntaxToken readonlyToken = SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTriviaFrom(constToken);

        // Replace the const token with readonly token
        SyntaxToken[] newSyntaxTokens =
        [
            readonlyToken,
            SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword),
        ];

        // Create a new modifiers list, replacing the const token with the static readonly tokens
        SyntaxTokenList newModifiers = modifiers.ReplaceRange(constToken, newSyntaxTokens);

        // Create the new field declaration with readonly modifiers
        FieldDeclarationSyntax readonlyDeclaration = constDeclaration.WithModifiers(newModifiers);

        // Get the root and replace the node
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode? newRoot = root?.ReplaceNode(constDeclaration, readonlyDeclaration);

        // Ensure newRoot is not null before returning the new solution
        if (newRoot == null)
        {
            throw new InvalidOperationException("Failed to replace node in the syntax tree.");
        }

        return document.WithSyntaxRoot(newRoot).Project.Solution;
    }
}

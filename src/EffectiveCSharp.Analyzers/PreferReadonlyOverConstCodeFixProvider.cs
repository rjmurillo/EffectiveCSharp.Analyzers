﻿namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="PreferReadonlyOverConstAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferReadonlyOverConstCodeFixProvider))]
[Shared]
public class PreferReadonlyOverConstCodeFixProvider : CodeFixProvider
{
    private static readonly string Title = "Use readonly instead of const";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.PreferReadonlyOverConst);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        Diagnostic diagnostic = context.Diagnostics[0];
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
        SyntaxToken constToken = default;
        for (int i = 0; i < modifiers.Count; i++)
        {
            SyntaxToken modifier = modifiers[i];
            if (modifier.IsKind(SyntaxKind.ConstKeyword))
            {
                constToken = modifier;
                break;
            }
        }

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
        return newRoot == null
            ? document.Project.Solution
            : document.WithSyntaxRoot(newRoot).Project.Solution;
    }
}

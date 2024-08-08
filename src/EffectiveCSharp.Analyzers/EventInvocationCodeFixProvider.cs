namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="EventInvocationAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EventInvocationCodeFixProvider))]
[Shared]
public class EventInvocationCodeFixProvider : CodeFixProvider
{
    private static readonly string Title = "Use null-conditional operator";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.UseNullConditionalOperatorForEventInvocations);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;
        SyntaxNode? node = root?.FindNode(diagnosticSpan);
        InvocationExpressionSyntax? invocationExpression = node?.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        IfStatementSyntax? ifStatement = node?.FirstAncestorOrSelf<IfStatementSyntax>();

        if (invocationExpression != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => UseNullConditionalOperatorAsync(context.Document, invocationExpression, c),
                    equivalenceKey: Title),
                diagnostic);
        }
        else if (ifStatement != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => UseNullConditionalOperatorAsync(context.Document, ifStatement, c),
                    equivalenceKey: Title),
                diagnostic);
        }
    }

    private static async Task<Document> UseNullConditionalOperatorAsync(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken)
    {
        DocumentEditor? editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        IdentifierNameSyntax identifierName = (IdentifierNameSyntax)invocationExpression.Expression;
        ConditionalAccessExpressionSyntax newInvocation = SyntaxFactory.ConditionalAccessExpression(
            identifierName,
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Invoke")),
                invocationExpression.ArgumentList));

        editor.ReplaceNode(invocationExpression, newInvocation);
        return editor.GetChangedDocument();
    }

    private static async Task<Document> UseNullConditionalOperatorAsync(Document document, IfStatementSyntax ifStatement, CancellationToken cancellationToken)
    {
        DocumentEditor? editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Check if the if statement follows the pattern: if (handler != null) handler(this, counter);
        ExpressionStatementSyntax? expressionStatement = ifStatement.Statement as ExpressionStatementSyntax;

        if (expressionStatement?.Expression is not InvocationExpressionSyntax invocationExpression)
        {
            return document;
        }

        // Identify the event handler variable (handler in this case)
        IdentifierNameSyntax? identifierName = invocationExpression.Expression as IdentifierNameSyntax
                                               ?? (invocationExpression.Expression as MemberAccessExpressionSyntax)?.Expression as IdentifierNameSyntax;

        if (identifierName == null)
        {
            return document;
        }

        // Attempt to find a preceding variable declaration if it exists
        BlockSyntax? blockSyntax = ifStatement.Parent as BlockSyntax;
        LocalDeclarationStatementSyntax? variableDeclaration = blockSyntax?.Statements
            .OfType<LocalDeclarationStatementSyntax>()
            .FirstOrDefault(v => v.Declaration.Variables.Any(var => var.Identifier.Text == identifierName.Identifier.Text));

        if (variableDeclaration == null)
        {
            // If there is no preceding variable declaration, replace the if statement directly
            ExpressionStatementSyntax newInvocation = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.ConditionalAccessExpression(
                        identifierName,
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Invoke")),
                            invocationExpression.ArgumentList)))
                .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
                .WithTrailingTrivia(ifStatement.GetTrailingTrivia());

            editor.ReplaceNode(ifStatement, newInvocation);
            return editor.GetChangedDocument();
        }

        // Get the actual event handler (e.g., Updated)
        if (variableDeclaration.Declaration.Variables.First().Initializer?.Value is IdentifierNameSyntax eventHandlerIdentifier)
        {
            // Create the null-conditional invocation using the event handler directly
            ExpressionStatementSyntax newInvocation = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.ConditionalAccessExpression(
                        eventHandlerIdentifier,
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Invoke")),
                            invocationExpression.ArgumentList)))
                .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
                .WithTrailingTrivia(ifStatement.GetTrailingTrivia());

            // Remove the variable declaration
            editor.RemoveNode(variableDeclaration);

            // Replace the if statement with the new invocation
            editor.ReplaceNode(ifStatement, newInvocation);

            return editor.GetChangedDocument();
        }

        return document;
    }
}

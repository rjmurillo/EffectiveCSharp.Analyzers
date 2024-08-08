namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="PreferExplicitTypesOnNumbersAnalyzer"/>.
/// </summary>
/// <seealso cref="CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferExplicitTypesForNumbersCodeFixProvider))]
[Shared]
public class PreferExplicitTypesForNumbersCodeFixProvider : CodeFixProvider
{
    private static readonly string Title = "Use explicit type";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.PreferImplicitlyTypedLocalVariables);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics.First();
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        if (root is null)
        {
            return;
        }

        LocalDeclarationStatementSyntax? declaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<LocalDeclarationStatementSyntax>().First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: c => UseExplicitTypeAsync(context.Document, declaration, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> UseExplicitTypeAsync(Document document, LocalDeclarationStatementSyntax? localDeclaration, CancellationToken cancellationToken)
    {
        if (localDeclaration is null)
        {
            return document;
        }

        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        VariableDeclaratorSyntax? variable = localDeclaration.Declaration.Variables.First();
        ExpressionSyntax? initializer = variable.Initializer?.Value;

        if (initializer is null)
        {
            return document;
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(initializer, cancellationToken);
        ITypeSymbol? type = typeInfo.ConvertedType;

        if (type is null)
        {
            return document;
        }

        TypeSyntax explicitType = SyntaxFactory
                .ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                .WithTriviaFrom(localDeclaration.Declaration.Type);

        LocalDeclarationStatementSyntax newDeclaration = localDeclaration.WithDeclaration(
            localDeclaration.Declaration.WithType(explicitType)
                .WithVariables(SyntaxFactory.SingletonSeparatedList(variable)));

        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        SyntaxNode newRoot = root.ReplaceNode(localDeclaration, newDeclaration);

        return document.WithSyntaxRoot(newRoot);
    }
}

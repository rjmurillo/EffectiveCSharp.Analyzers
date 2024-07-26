using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace EffectiveCSharp.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferExplicitTypesForNumbersCodeFixProvider)), Shared]
public class PreferExplicitTypesForNumbersCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use explicit type";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.PreferImplicitlyTypedLocalVariables);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<LocalDeclarationStatementSyntax>().First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: c => UseExplicitTypeAsync(context.Document, declaration, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private async Task<Document> UseExplicitTypeAsync(Document document, LocalDeclarationStatementSyntax localDeclaration, CancellationToken cancellationToken)
    {
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        VariableDeclaratorSyntax variable = localDeclaration.Declaration.Variables.First();
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

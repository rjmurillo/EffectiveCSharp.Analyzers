namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="PreferMemberInitializersToAssignmentStatementsAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferMemberInitializersToAssignmentStatementsCodeFixProvider))]
[Shared]
public class PreferMemberInitializersToAssignmentStatementsCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
        DiagnosticIds.PreferMemberInitializersToAssignmentStatement,
        DiagnosticIds.PreferMemberInitializersExceptNullOrZero,
        DiagnosticIds.PreferMemberInitializersExceptWhenVaryingInitializations);

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            SyntaxNode declaration = root.FindNode(diagnosticSpan);

            switch (diagnostic.Id)
            {
                case DiagnosticIds.PreferMemberInitializersToAssignmentStatement:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Use member initializer",
                            createChangedSolution: cancellationToken => ReplaceAssignmentWithMemberInitializerAsync(context.Document, declaration, cancellationToken),
                            equivalenceKey: "Use member initializer"),
                        diagnostic);

                    break;
                case DiagnosticIds.PreferMemberInitializersExceptNullOrZero:
                    break;
                case DiagnosticIds.PreferMemberInitializersExceptWhenVaryingInitializations:
                    break;
                default:
                    continue;
            }
        }
    }

    private static async Task<Solution> ReplaceAssignmentWithMemberInitializerAsync(Document document, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        if (declaration is FieldDeclarationSyntax fieldDeclaration)
        {
            VariableDeclarationSyntax variableDeclaration = fieldDeclaration.Declaration;
            VariableDeclaratorSyntax variable = variableDeclaration.Variables[0];
            SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            IFieldSymbol? fieldSymbol = semanticModel?.GetDeclaredSymbol(variable, cancellationToken) as IFieldSymbol;

            if (fieldSymbol is not null)
            {
                EqualsValueClauseSyntax? newInitializer = GetInitializerForType(fieldSymbol.Type);

                if (newInitializer != null)
                {
                    VariableDeclaratorSyntax variableDeclaratorWithInitializer = variable.WithInitializer(newInitializer);

                    VariableDeclarationSyntax variableDeclarationWithInitializer = variableDeclaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variableDeclaratorWithInitializer));

                    FieldDeclarationSyntax fieldDeclarationWithInitializer = fieldDeclaration.WithDeclaration(variableDeclarationWithInitializer);

                    SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

                    return root == null
                        ? document.Project.Solution
                        : document.WithSyntaxRoot(root.ReplaceNode(fieldDeclaration, fieldDeclarationWithInitializer)).Project.Solution;
                }
            }
        }

        return document.Project.Solution;
    }

    private static EqualsValueClauseSyntax? GetInitializerForType(ITypeSymbol fieldType)
    {
        if (fieldType.TypeKind == TypeKind.Interface)
        {
            return null;
        }

        if (fieldType.SpecialType == SpecialType.System_String)
        {
            return SyntaxFactory.EqualsValueClause(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("string"), SyntaxFactory.IdentifierName("Empty")));
        }

        if (fieldType is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
        {
            string genericTypeName = namedTypeSymbol.Name;
            string genericArguments = string.Join(", ", namedTypeSymbol.TypeArguments.Select(arg => arg.ToDisplayString()));
            string fullTypeName = $"{genericTypeName}<{genericArguments}>";
            return SyntaxFactory.EqualsValueClause(SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(fullTypeName)).WithArgumentList(SyntaxFactory.ArgumentList()));
        }

        return SyntaxFactory.EqualsValueClause(SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(fieldType.Name)).WithArgumentList(SyntaxFactory.ArgumentList()));
    }
}

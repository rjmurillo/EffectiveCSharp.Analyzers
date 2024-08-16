namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="PreferMemberInitializersToAssignmentStatementsAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferMemberInitializersToAssignmentStatementsCodeFixProvider))]
[Shared]
public class PreferMemberInitializersToAssignmentStatementsCodeFixProvider : CodeFixProvider
{
    private static readonly IEqualityComparer<IFieldSymbol> _fieldSymbolNameComparer = new FieldSymbolNameComparer();

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
        DiagnosticIds.PreferMemberInitializersToAssignmentStatement,
        DiagnosticIds.PreferMemberInitializersExceptNullOrZero,
        DiagnosticIds.PreferMemberInitializersExceptWhenVaryingInitializations,
        DiagnosticIds.PreferMemberInitializersWhenNoInitializationPresent);

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
        {
            return;
        }

        Dictionary<IFieldSymbol, IList<ExpressionStatementSyntax>> initializationsToFixPerField = new Dictionary<IFieldSymbol, IList<ExpressionStatementSyntax>>(_fieldSymbolNameComparer);

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            SyntaxNode declaration = root.FindNode(diagnosticSpan);

            switch (diagnostic.Id)
            {
                case DiagnosticIds.PreferMemberInitializersToAssignmentStatement:
                    if (declaration is ExpressionStatementSyntax expressionStatement && expressionStatement.Expression is AssignmentExpressionSyntax assignmentExpressionSyntax)
                    {
                        // Get symbol info for the expression
                        ISymbol? symbol = semanticModel.GetSymbolInfo(assignmentExpressionSyntax.Left, context.CancellationToken).Symbol;
                        IFieldSymbol? fieldSymbol = symbol as IFieldSymbol;

                        if (fieldSymbol is not null)
                        {
                            if (initializationsToFixPerField.TryGetValue(fieldSymbol, out IList<ExpressionStatementSyntax> initializations))
                            {
                                initializations.Add(expressionStatement);
                            }
                            else
                            {
                                initializationsToFixPerField.Add(fieldSymbol, new List<ExpressionStatementSyntax> { expressionStatement });
                            }
                        }
                    }

                    break;
                case DiagnosticIds.PreferMemberInitializersExceptNullOrZero:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Do not initialize to null or zero",
                            createChangedSolution: cancellationToken => EnforceFieldIsNotInitializedAsync(context.Document, declaration, cancellationToken),
                            equivalenceKey: "Use member initializer"),
                        diagnostic);
                    break;
                case DiagnosticIds.PreferMemberInitializersExceptWhenVaryingInitializations:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Do not use field initializer when varying initializations",
                            createChangedSolution: cancellationToken => EnforceFieldIsNotInitializedAsync(context.Document, declaration, cancellationToken),
                            equivalenceKey: "Use member initializer"),
                        diagnostic);
                    break;
                case DiagnosticIds.PreferMemberInitializersWhenNoInitializationPresent:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Use initializer in field declaration",
                            createChangedSolution: cancellationToken => EnforceFieldDeclarationInitializationAsync(context.Document, declaration, cancellationToken),
                            equivalenceKey: "Use member initializer"),
                        diagnostic);
                    break;
                default:
                    continue;
            }
        }

        foreach (IFieldSymbol fieldSymbol in initializationsToFixPerField.Keys)
        {
            if (initializationsToFixPerField.TryGetValue(fieldSymbol, out IList<ExpressionStatementSyntax> initializations))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Initialize in field declaration instead",
                        createChangedSolution: cancellationToken => ReplaceAssignmentsWithFieldDeclarationInitializerAsync(context.Document, fieldSymbol, initializations, cancellationToken),
                        equivalenceKey: "Use member initializer"),
                    context.Diagnostics);
            }
        }
    }

    private static async Task<Solution> EnforceFieldIsNotInitializedAsync(Document document, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is not null && declaration is FieldDeclarationSyntax fieldDeclaration)
        {
            FieldDeclarationSyntax? newFieldDeclaration = CreateFieldDeclaration(fieldDeclaration);

            if (newFieldDeclaration is not null)
            {
                return document.WithSyntaxRoot(root.ReplaceNode(fieldDeclaration, newFieldDeclaration)).Project.Solution;
            }
        }

        return document.Project.Solution;
    }

    private static async Task<Solution> EnforceFieldDeclarationInitializationAsync(Document document, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is not null && declaration is FieldDeclarationSyntax fieldDeclaration)
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
                    FieldDeclarationSyntax? fieldDeclarationWithInitializer = CreateFieldDeclaration(fieldDeclaration, newInitializer);

                    if (fieldDeclarationWithInitializer is not null)
                    {
                        return document.WithSyntaxRoot(root.ReplaceNode(fieldDeclaration, fieldDeclarationWithInitializer)).Project.Solution;
                    }
                }
            }
        }

        return document.Project.Solution;
    }

    private static async Task<Solution> ReplaceAssignmentsWithFieldDeclarationInitializerAsync(Document document, IFieldSymbol fieldSymbol, IList<ExpressionStatementSyntax> declaration, CancellationToken cancellationToken)
    {
        if (declaration.Count == 0)
        {
            return document.Project.Solution;
        }

        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is not null)
        {
            EqualsValueClauseSyntax? newInitializer = GetInitializerForType(fieldSymbol.Type);

            if (newInitializer != null)
            {
                SyntaxNode? newRoot = root.RemoveNodes(declaration, SyntaxRemoveOptions.KeepNoTrivia);

                if (newRoot is not null)
                {
                    FieldDeclarationSyntax? existingFieldDeclaration = (FieldDeclarationSyntax?)(await fieldSymbol.DeclaringSyntaxReferences.First().GetSyntaxAsync(cancellationToken).ConfigureAwait(false))?.Parent?.Parent;

                    if (existingFieldDeclaration is not null)
                    {
                        FieldDeclarationSyntax? fieldDeclarationWithInitializer = CreateFieldDeclaration(existingFieldDeclaration, newInitializer);

                        if (fieldDeclarationWithInitializer is not null)
                        {
                            return document.WithSyntaxRoot(newRoot.ReplaceNode(existingFieldDeclaration, fieldDeclarationWithInitializer)).Project.Solution;
                        }
                    }
                }
            }
        }

        return document.Project.Solution;
    }

    private static FieldDeclarationSyntax? CreateFieldDeclaration(FieldDeclarationSyntax existingFieldDeclaration, EqualsValueClauseSyntax? newInitializer = null)
    {
        // Extract the existing variable declarator
        VariableDeclaratorSyntax? existingVariableDeclarator = existingFieldDeclaration.Declaration.Variables.FirstOrDefault();

        if (existingVariableDeclarator is null)
        {
            return null;
        }

        // Create a new variable declarator with the initializer
        VariableDeclaratorSyntax newVariableDeclarator = SyntaxFactory.VariableDeclarator(existingVariableDeclarator.Identifier)
            .WithInitializer(newInitializer);

        // Create a new variable declaration with the new variable declarator
        VariableDeclarationSyntax newVariableDeclaration = SyntaxFactory.VariableDeclaration(existingFieldDeclaration.Declaration.Type)
            .WithVariables(SyntaxFactory.SingletonSeparatedList(newVariableDeclarator));

        // Create a new field declaration with the new variable declaration and existing modifiers
        return SyntaxFactory.FieldDeclaration(newVariableDeclaration)
            .WithModifiers(existingFieldDeclaration.Modifiers);
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

    private sealed class FieldSymbolNameComparer : IEqualityComparer<IFieldSymbol>
    {
        public bool Equals(IFieldSymbol? x, IFieldSymbol? y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            return string.Equals(x.Name, y.Name);
        }

        public int GetHashCode(IFieldSymbol obj)
        {
            return StringComparer.Ordinal.GetHashCode(obj.Name);
        }
    }
}

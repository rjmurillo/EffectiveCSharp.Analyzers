namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="PreferDeclarationInitializersToAssignmentStatementsAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferDeclarationInitializersToAssignmentStatementsCodeFixProvider))]
[Shared]
public class PreferDeclarationInitializersToAssignmentStatementsCodeFixProvider : CodeFixProvider
{
    private static readonly string EquivalenceKey = "ECS1200CodeFix";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
        DiagnosticIds.PreferDeclarationInitializersToAssignmentStatement,
        DiagnosticIds.PreferDeclarationInitializersExceptNullOrZero,
        DiagnosticIds.PreferDeclarationInitializersExceptWhenVaryingInitializations,
        DiagnosticIds.PreferDeclarationInitializersWhenNoInitializationPresent);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            SyntaxNode diagnosticNode = root.FindNode(diagnosticSpan);

            switch (diagnostic.Id)
            {
                case DiagnosticIds.PreferDeclarationInitializersToAssignmentStatement:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Initialize in field declaration instead",
                            createChangedSolution: cancellationToken => ReplaceAssignmentsWithFieldDeclarationInitializerAsync(context, semanticModel, diagnosticNode, cancellationToken),
                            equivalenceKey: EquivalenceKey),
                        context.Diagnostics);
                    break;
                case DiagnosticIds.PreferDeclarationInitializersExceptNullOrZero:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Do not initialize to null or zero",
                            createChangedSolution: cancellationToken => EnforceFieldIsNotInitializedAsync(context.Document, diagnosticNode, cancellationToken),
                            equivalenceKey: EquivalenceKey),
                        diagnostic);
                    break;
                case DiagnosticIds.PreferDeclarationInitializersExceptWhenVaryingInitializations:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Do not use field initializer when varying initializations",
                            createChangedSolution: cancellationToken => EnforceFieldIsNotInitializedAsync(context.Document, diagnosticNode, cancellationToken),
                            equivalenceKey: EquivalenceKey),
                        diagnostic);
                    break;
                case DiagnosticIds.PreferDeclarationInitializersWhenNoInitializationPresent:
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Use initializer in field declaration",
                            createChangedSolution: cancellationToken => EnforceFieldDeclarationInitializationAsync(context.Document, semanticModel, diagnosticNode, cancellationToken),
                            equivalenceKey: EquivalenceKey),
                        diagnostic);
                    break;
                default:
                    continue;
            }
        }
    }

    private static async Task<Solution> ReplaceAssignmentsWithFieldDeclarationInitializerAsync(CodeFixContext context, SemanticModel semanticModel, SyntaxNode diagnosticNode, CancellationToken cancellationToken)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null
            || diagnosticNode.Parent is not AssignmentExpressionSyntax assignmentExpression // The diagnostic should be an expression statement
            || assignmentExpression.Parent is not ExpressionStatementSyntax expressionStatementSyntax
            || semanticModel.GetSymbolInfo(assignmentExpression.Left, context.CancellationToken).Symbol is not IFieldSymbol fieldSymbol // The left side of the assignment should be a field
            || fieldSymbol.DeclaringSyntaxReferences.Length != 1 // The field should have a single declaration
            || fieldSymbol.DeclaringSyntaxReferences[0] is not { } syntaxReference // Let's get a reference to the field declaration
            || (await syntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false)).Parent?.Parent is not FieldDeclarationSyntax existingFieldDeclaration // Let's get the field declaration
            || root.RemoveNode(expressionStatementSyntax, SyntaxRemoveOptions.KeepNoTrivia) is not { } newRoot // Let's remove the assignment statement since we've found the field and have an initializer
            || CreateFieldDeclaration(existingFieldDeclaration, GetInitializerFromExpressionSyntax(assignmentExpression.Right)) is not { } fieldDeclarationWithNewInitializer)
        {
            // If any of the above conditions are not met, return the current solution
            return context.Document.Project.Solution;
        }

        // Replace the existing field declaration with the new field declaration
        return context.Document.WithSyntaxRoot(newRoot.ReplaceNode(existingFieldDeclaration, fieldDeclarationWithNewInitializer)).Project.Solution;
    }

    private static EqualsValueClauseSyntax GetInitializerFromExpressionSyntax(ExpressionSyntax assignmentExpression)
    {
        // We use a given expression syntax node, duplicate it, and create an EqualsValueClauseSyntax node from it
        // which can serve as an initializer for a field declaration
        return SyntaxFactory.EqualsValueClause(assignmentExpression.WithTriviaFrom(assignmentExpression));
    }

    private static async Task<Solution> EnforceFieldIsNotInitializedAsync(Document document, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null
            || declaration is not FieldDeclarationSyntax fieldDeclaration // The declaration should be a field declaration
            || CreateFieldDeclaration(fieldDeclaration) is not { } newFieldDeclaration)
        {
            // If any of the above conditions are not met, return the current solution
            return document.Project.Solution;
        }

        // Replace the existing field declaration with the new field declaration
        return document.WithSyntaxRoot(root.ReplaceNode(fieldDeclaration, newFieldDeclaration)).Project.Solution;
    }

    private static async Task<Solution> EnforceFieldDeclarationInitializationAsync(Document document,  SemanticModel semanticModel, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null
            || declaration is not FieldDeclarationSyntax fieldDeclaration // The declaration should be a field declaration
            || fieldDeclaration.Declaration.Variables.Count != 1 // The field declaration should have a variable declarator
            || fieldDeclaration.Declaration.Variables[0] is not { } variableDeclarator // The field declaration should have a variable declarator
            || semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken) is not IFieldSymbol fieldSymbol // Let's get the field symbol
            || GetDefaultInitializerForType(fieldSymbol.Type) is not { } newInitializer // Let's try to get an initializer for the field type
            || CreateFieldDeclaration(fieldDeclaration, newInitializer) is not { } fieldDeclarationWithInitializer)
        {
            // If any of the above conditions are not met, return the current solution
            return document.Project.Solution;
        }

        // Replace the existing field declaration with the new field declaration
        return document.WithSyntaxRoot(root.ReplaceNode(fieldDeclaration, fieldDeclarationWithInitializer)).Project.Solution;
    }

    private static EqualsValueClauseSyntax? GetDefaultInitializerForType(ITypeSymbol fieldType)
    {
        // If the field type is an interface or delegate, we do not want to initialize it
        if (fieldType.TypeKind == TypeKind.Interface
            || fieldType.TypeKind == TypeKind.Delegate)
        {
            return null;
        }

        // If the field type is a string, we can initialize it to string.Empty
        if (fieldType.SpecialType == SpecialType.System_String)
        {
            return SyntaxFactory.EqualsValueClause(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("string"), SyntaxFactory.IdentifierName("Empty")));
        }

        // If the field type is a generic type, we need to create a generic object creation expression
        if (fieldType is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol)
        {
            string genericTypeName = namedTypeSymbol.Name;
            string genericArguments = string.Join(", ", namedTypeSymbol.TypeArguments.Select(arg => arg.ToDisplayString()));
            string fullTypeName = $"{genericTypeName}<{genericArguments}>";
            return SyntaxFactory.EqualsValueClause(SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(fullTypeName)).WithArgumentList(SyntaxFactory.ArgumentList()));
        }

        return SyntaxFactory.EqualsValueClause(SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(fieldType.Name)).WithArgumentList(SyntaxFactory.ArgumentList()));
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
}

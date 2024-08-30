namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #12 - Prefer member initializers to assignment statements.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferMemberInitializerAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString FieldDeclaration = "field declaration";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.PreferDeclarationInitializersToAssignmentStatement,
        title: "Prefer member initializers to assignment statements",
        messageFormat: "Use a {0} initializer on '{1}' instead of an assignment statement",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Avoid member variables and type constructors from getting out of sync by initializing variables where you declare them.",
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticIds.PreferDeclarationInitializersToAssignmentStatement}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register for constructor, method, property setter, and field initializer syntax nodes
        context.RegisterSyntaxNodeAction(
            AnalyzeNode,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        switch (context.Node)
        {
            case ConstructorDeclarationSyntax constructor:
                AnalyzeConstructor(context, constructor);
                break;
            case FieldDeclarationSyntax field:
                AnalyzeField(context, field);
                break;
        }
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax fieldDeclaration)
    {
        foreach (VariableDeclaratorSyntax variable in fieldDeclaration.Declaration.Variables)
        {
            EqualsValueClauseSyntax? initializer = variable.Initializer;

            if (initializer == null)
            {
                continue;
            }

            // If the field is initialized in the declaration, track it for potential redundant initializations in constructors
            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not IFieldSymbol fieldSymbol)
            {
                continue;
            }

            // Handle cases where the 'default' literal is used directly (e.g., `default` or `default(int)`)
            if (initializer.Value.IsKind(SyntaxKind.DefaultLiteralExpression))
            {
                continue; // 'default' literal always indicates default initialization and is okay for fields
            }

            if (context.IsDefaultInitialization(fieldSymbol.Type, initializer.Value))
            {
                // Report a diagnostic if the field is being initialized to a redundant default value
                Diagnostic diagnostic = variable.GetLocation().CreateDiagnostic(Rule, FieldDeclaration, fieldSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor)
    {
        if (constructor.Body == null)
        {
            return;
        }

        for (int i = 0; i < constructor.Body.Statements.Count; i++)
        {
            if (constructor.Body.Statements[i] is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
            {
                continue;
            }

            AnalyzeAssignment(context, assignment);
        }
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment)
    {
        if (assignment.Left is not IdentifierNameSyntax identifierName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol is not IFieldSymbol fieldSymbol
            || fieldSymbol.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        VariableDeclaratorSyntax? fieldDeclaration = fieldSymbol.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) as VariableDeclaratorSyntax;

        // Skip if the field is already initialized in the declaration with the same value and
        // check if the initializer value in the declaration is the same as in the constructor assignment
        if (fieldDeclaration?.Initializer != null
            && context.SemanticModel.AreExpressionsEquivalent(fieldDeclaration.Initializer.Value, assignment.Right))
        {
            // Ensure we only trigger a diagnostic if the constructor has no parameters,
            // or if the assignment is not dependent on a constructor parameter or a method call.
            if (!context.SemanticModel.IsInitializedFromConstructorParameter(assignment.Right)
                && !context.SemanticModel.IsInitializedFromMethodCall(assignment.Right)
                && !context.SemanticModel.IsInitializedFromInstanceMember(assignment.Right))
            {
                Diagnostic d = assignment.GetLocation().CreateDiagnostic(Rule, FieldDeclaration, fieldSymbol.Name);
                context.ReportDiagnostic(d);
            }

            return;
        }

        // Ensure the assignment is not redundant (check if it matches the default value)
        if (context.IsDefaultInitialization(fieldSymbol.Type, assignment.Right))
        {
            return;
        }

        // Ensure the field is not being initialized with a constructor parameter or method call
        if (context.SemanticModel.IsInitializedFromConstructorParameter(assignment.Right)
            || context.SemanticModel.IsInitializedFromMethodCall(assignment.Right))
        {
            return;
        }

        // If the assignment does not match any of the conditions, report it as redundant
        Diagnostic diagnostic = identifierName.GetLocation().CreateDiagnostic(Rule, FieldDeclaration, fieldSymbol.Name);
        context.ReportDiagnostic(diagnostic);
    }
}

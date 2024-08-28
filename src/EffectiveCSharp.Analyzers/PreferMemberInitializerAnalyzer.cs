namespace EffectiveCSharp.Analyzers;

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
            SyntaxKind.PropertyDeclaration,
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
            case PropertyDeclarationSyntax property:
                AnalyzeProperty(context, property);
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

            // TODO: track fields initialized in the declaration and check them later against constructor assignments?
            if (!IsDefaultInitialization(fieldSymbol.Type, initializer.Value, context.SemanticModel))
            {
                continue;
            }

            // Report a diagnostic if the field is being initialized to a redundant default value
            Diagnostic diagnostic = variable.GetLocation().CreateDiagnostic(Rule, FieldDeclaration, fieldSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }


    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor)
    {
        if (constructor.Body == null)
        {
            return;
        }

        foreach (ExpressionStatementSyntax? statement in constructor.Body.Statements.OfType<ExpressionStatementSyntax>())
        {
            if (statement.Expression is AssignmentExpressionSyntax assignment)
            {
                AnalyzeAssignment(context, assignment, constructor);
            }
        }
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property)
    {
        if (property.AccessorList == null)
        {
            return;
        }

        foreach (AccessorDeclarationSyntax accessor in property.AccessorList.Accessors)
        {
            if (accessor.Body == null)
            {
                continue;
            }

            foreach (ExpressionStatementSyntax? statement in accessor.Body.Statements.OfType<ExpressionStatementSyntax>())
            {
                if (statement.Expression is AssignmentExpressionSyntax assignment)
                {
                    AnalyzeAssignment(context, assignment, property);
                }
            }
        }
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment, SyntaxNode parentNode)
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

        if (fieldDeclaration?.Initializer != null)
        {
            return;
        }

        if (IsDefaultInitialization(fieldSymbol.Type, assignment.Right, context.SemanticModel))
        {
            return;
        }

        if (!IsInitializedFromConstructorParameter(assignment.Right, context.SemanticModel))
        {
            // TODO: Determine what type for the message
            Diagnostic diagnostic = identifierName.GetLocation().CreateDiagnostic(Rule, fieldSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsDefaultInitialization(ITypeSymbol fieldType, ExpressionSyntax right, SemanticModel semanticModel)
    {
        // Handle numeric types (int, double, etc.)
        if (fieldType.IsValueType)
        {
            Optional<object?> defaultValue = semanticModel.GetConstantValue(right);
            if (defaultValue.HasValue && (defaultValue.Value is 0 || defaultValue.Value.Equals(Activator.CreateInstance(fieldType.GetType()))))
            {
                return true;
            }
        }

        // Handle string types
        if (fieldType.SpecialType == SpecialType.System_String && right.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return ((LiteralExpressionSyntax)right).Token.ValueText == string.Empty;
        }

        // Handle default expressions
        if (right.IsKind(SyntaxKind.DefaultExpression))
        {
            ITypeSymbol? expressionType = semanticModel.GetTypeInfo(right).Type;
            return expressionType?.Equals(fieldType, SymbolEqualityComparer.Default) == true;
        }

        return false;
    }

    private static bool IsInitializedFromConstructorParameter(ExpressionSyntax right, SemanticModel semanticModel)
    {
        IOperation? operation = semanticModel.GetOperation(right);

        if (operation is IParameterReferenceOperation)
        {
            return true;
        }

        if (operation is ILocalReferenceOperation localReference)
        {
            ILocalSymbol localSymbol = localReference.Local;
            VariableDeclaratorSyntax? localDeclaration = localSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as VariableDeclaratorSyntax;

            if (localDeclaration?.Initializer?.Value != null)
            {
                IOperation? initializerOperation = semanticModel.GetOperation(localDeclaration.Initializer.Value);
                return initializerOperation is IParameterReferenceOperation;
            }
        }

        return false;
    }
}

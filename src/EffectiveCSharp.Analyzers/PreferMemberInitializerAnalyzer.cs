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

            if (IsDefaultInitialization(fieldSymbol.Type, initializer.Value, context.SemanticModel))
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

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property)
    {
        if (property.AccessorList == null)
        {
            return;
        }

        for (int a = 0; a < property.AccessorList.Accessors.Count; a++)
        {
            AccessorDeclarationSyntax accessor = property.AccessorList.Accessors[a];

            if (accessor.Body == null)
            {
                continue;
            }

            for (int i = 0; i < accessor.Body.Statements.Count; i++)
            {
                if (accessor.Body.Statements[i] is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
                {
                    continue;
                }

                AnalyzeAssignment(context, assignment);
            }
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

        // Skip if the field is already initialized in the declaration
        if (fieldDeclaration?.Initializer != null)
        {
            return;
        }

        // Ensure the assignment is not redundant (check if it matches the default value)
        if (IsDefaultInitialization(fieldSymbol.Type, assignment.Right, context.SemanticModel))
        {
            return;
        }

        // Ensure the field is not being initialized with a constructor parameter or method call
        if (!IsInitializedFromConstructorParameter(assignment.Right, context.SemanticModel)
            && !IsInitializedFromMethodCall(assignment.Right, context.SemanticModel))
        {
            Diagnostic diagnostic = identifierName.GetLocation().CreateDiagnostic(Rule, FieldDeclaration, fieldSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsInitializedFromMethodCall(ExpressionSyntax right, SemanticModel semanticModel)
    {
        // Check if the right side of the assignment is a method call
        return semanticModel.GetOperation(right) is IInvocationOperation;
    }

    private static bool IsDefaultInitialization(ITypeSymbol fieldType, ExpressionSyntax right, SemanticModel semanticModel)
    {
        // Handle default keyword
        if (right.IsKind(SyntaxKind.DefaultExpression))
        {
            TypeInfo typeInfo = semanticModel.GetTypeInfo(right);
            return typeInfo.Type?.Equals(fieldType, SymbolEqualityComparer.IncludeNullability) == true;
        }

        // Handle cases where the 'default' literal is used directly (e.g., `default` or `default(int)`)
        if (right.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            return true; // 'default' literal always indicates default initialization
        }

        // Handle numeric types (int, double, etc.)
        if (fieldType.IsValueType)
        {
            Optional<object?> defaultValue = semanticModel.GetConstantValue(right);
            if (defaultValue.HasValue && IsDefaultValue(defaultValue.Value, fieldType))
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
            return expressionType?.Equals(fieldType, SymbolEqualityComparer.IncludeNullability) == true;
        }

        return false;
    }

    private static bool IsDefaultValue(object? value, ITypeSymbol fieldType)
    {
        if (value == null)
        {
            return false;
        }

        try
        {
            switch (fieldType.SpecialType)
            {
                // Handle numeric conversions
                case SpecialType.System_Double when Convert.ToDouble(value) == 0.0:
                case SpecialType.System_Single when Convert.ToSingle(value) == 0.0f:
                case SpecialType.System_Int32 when Convert.ToInt32(value) == 0:
                case SpecialType.System_Int64 when Convert.ToInt64(value) == 0L:
                case SpecialType.System_Int16 when Convert.ToInt16(value) == 0:
                case SpecialType.System_Byte when Convert.ToByte(value) == 0:
                // Handle other types like boolean, char, etc.
                case SpecialType.System_Boolean when value is bool and false:
                case SpecialType.System_Char when value is char and '\0':
                    return true;
                default:
                    return false;
            }
        }
        catch (InvalidCastException)
        {
            return false; // If conversion fails, it's not the default value.
        }
        catch (FormatException)
        {
            return false; // If conversion fails, it's not the default value.
        }
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

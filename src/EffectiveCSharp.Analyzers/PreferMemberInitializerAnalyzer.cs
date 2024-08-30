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

            // Handle cases where the 'default' literal is used directly (e.g., `default` or `default(int)`)
            if (initializer.Value.IsKind(SyntaxKind.DefaultLiteralExpression))
            {
                continue; // 'default' literal always indicates default initialization and is okay for fields
            }

            if (IsDefaultInitialization(fieldSymbol.Type, initializer.Value, context.SemanticModel, context.CancellationToken))
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

        // Skip if the field is already initialized in the declaration with the same value
        if (fieldDeclaration?.Initializer != null)
        {
            // Check if the initializer value in the declaration is the same as in the constructor assignment
            if (AreExpressionsEquivalent(fieldDeclaration.Initializer.Value, assignment.Right, context.SemanticModel))
            {
                // Ensure we only trigger a diagnostic if the constructor has no parameters,
                // or if the assignment is not dependent on a constructor parameter or a method call.
                if (!IsInitializedFromConstructorParameter(assignment.Right, context.SemanticModel)
                    && !IsInitializedFromMethodCall(assignment.Right, context.SemanticModel)
                    && !IsInitializedFromInstanceMember(assignment.Right, context.SemanticModel))
                {
                    Diagnostic d = assignment.GetLocation().CreateDiagnostic(Rule, FieldDeclaration, fieldSymbol.Name);
                    context.ReportDiagnostic(d);
                }

                return;
            }
        }

        // Ensure the assignment is not redundant (check if it matches the default value)
        if (IsDefaultInitialization(fieldSymbol.Type, assignment.Right, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        // Ensure the field is not being initialized with a constructor parameter or method call
        if (IsInitializedFromConstructorParameter(assignment.Right, context.SemanticModel)
            || IsInitializedFromMethodCall(assignment.Right, context.SemanticModel))
        {
            return;
        }

        // If the assignment does not match any of the conditions, report it as redundant
        Diagnostic diagnostic = identifierName.GetLocation().CreateDiagnostic(Rule, FieldDeclaration, fieldSymbol.Name);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsInitializedFromInstanceMember(ExpressionSyntax right, SemanticModel semanticModel)
    {
        IOperation? operation = semanticModel.GetOperation(right);

        if (operation is IObjectCreationOperation objectCreation)
        {
            // Check if the object initializer references any instance members
            foreach (IOperation? initializer in objectCreation.Initializer?.Initializers ?? Enumerable.Empty<IOperation>())
            {
                if (initializer is ISimpleAssignmentOperation { Value: IMemberReferenceOperation mro } && IsInstanceMemberOfContainingType(mro, semanticModel, right))
                {
                    return true;
                }
            }
        }

        // Check if the operation is a member reference and whether that member is an instance (non-static) member of the containing type.
        if (operation is IMemberReferenceOperation memberReference && IsInstanceMemberOfContainingType(memberReference, semanticModel, right))
        {
            return true;
        }

        // Also check for nested member access, such as accessing a field inside another field (e.g., `this.otherField.Field`)
        if (operation is IFieldReferenceOperation { Instance: not null, Field.IsStatic: false })
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified <see cref="IMemberReferenceOperation"/> refers to a non-static member
    /// of the containing type where the member reference is located.
    /// </summary>
    /// <param name="memberReferenceOperation">The member reference operation to evaluate.</param>
    /// <param name="semanticModel">An instance of <see cref="SemanticModel"/>.</param>
    /// <param name="right">The <see cref="ExpressionSyntax"/> to get the containing type.</param>
    /// <returns>
    /// <c>true</c> if the member referenced by <paramref name="memberReferenceOperation"/> is a non-static member
    /// of the containing type; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks if the member being referenced belongs to the same type that contains the operation
    /// and ensures that the member is not static.
    /// </remarks>

    private static bool IsInstanceMemberOfContainingType(IMemberReferenceOperation memberReferenceOperation, SemanticModel semanticModel, ExpressionSyntax right)
    {
        INamedTypeSymbol? containingType = semanticModel.GetEnclosingSymbol(right.SpanStart)?.ContainingType;

        if (containingType != null
            && memberReferenceOperation.Member.ContainingType.Equals(containingType, SymbolEqualityComparer.IncludeNullability)
            && !memberReferenceOperation.Member.IsStatic)
        {
            return true;
        }

        return false;
    }

    private static bool AreExpressionsEquivalent(ExpressionSyntax left, ExpressionSyntax right, SemanticModel semanticModel)
    {
        // This method checks if the two expressions represent the same value/initialization
        IOperation? leftOperation = semanticModel.GetOperation(left);
        IOperation? rightOperation = semanticModel.GetOperation(right);

        // Compare the operations for semantic equivalence
        return leftOperation != null && rightOperation != null && leftOperation.Kind == rightOperation.Kind && leftOperation.ConstantValue.Equals(rightOperation.ConstantValue);
    }

    private static bool IsInitializedFromMethodCall(ExpressionSyntax right, SemanticModel semanticModel)
    {
        // Check if the right side of the assignment is a method call
        return semanticModel.GetOperation(right) is IInvocationOperation;
    }

    private static bool IsDefaultInitialization(ITypeSymbol fieldType, ExpressionSyntax right, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // Handle default keyword
        if (right.IsKind(SyntaxKind.DefaultExpression))
        {
            TypeInfo typeInfo = semanticModel.GetTypeInfo(right, cancellationToken);
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
            Optional<object?> defaultValue = semanticModel.GetConstantValue(right, cancellationToken);
            if (defaultValue.HasValue && IsDefaultValue(defaultValue.Value, fieldType))
            {
                return true;
            }

            // Handle user-defined structs initialized with 'new'
            if (right is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0 } objectCreation
                && semanticModel.GetTypeInfo(objectCreation, cancellationToken).Type?.Equals(fieldType, SymbolEqualityComparer.IncludeNullability) == true)
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
            ITypeSymbol? expressionType = semanticModel.GetTypeInfo(right, cancellationToken).Type;
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

        // Check if the assignment directly involves a constructor parameter
        if (operation is IParameterReferenceOperation)
        {
            return true;
        }

        // Check for local variables initialized from constructor parameters (like dependency injection)
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

        // Check if the assignment involves an object creation where the constructor uses a parameter
        if (operation is IObjectCreationOperation objectCreation)
        {
            for (int i = 0; i < objectCreation.Arguments.Length; i++)
            {
                IArgumentOperation argument = objectCreation.Arguments[i];
                if (argument.Value is IParameterReferenceOperation)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

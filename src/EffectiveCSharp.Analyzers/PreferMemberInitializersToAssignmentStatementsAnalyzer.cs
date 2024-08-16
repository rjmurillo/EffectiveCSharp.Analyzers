namespace EffectiveCSharp.Analyzers;

/// <summary>
/// Analyzer that checks for the use of assignment statements in constructors when member initializers could be used instead.
/// </summary>
/// <seealso cref="DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferMemberInitializersToAssignmentStatementsAnalyzer : DiagnosticAnalyzer
{
    private const string HelpLinkUri = $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{DiagnosticIds.PreferMemberInitializersToAssignmentStatement}.md";

    private static readonly DiagnosticDescriptor GeneralRule = new(
        id: DiagnosticIds.PreferMemberInitializersToAssignmentStatement,
        title: "Prefer member initializers to assignment statements",
        messageFormat: "Use a member initializer instead of an assignment statement",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field initialization in a constructor that does not use an argument should be done with a member initializer.",
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor RuleExceptionInitializeToNullOrZero = new(
        id: DiagnosticIds.PreferMemberInitializersExceptNullOrZero,
        title: "Should not initialize to null or zero",
        messageFormat: "Do not initialize to null or zero as these already occur by default",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field initialization to null or zero is redundant and should be avoided.",
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor RuleExceptionShouldNotInitializeInDeclaration = new(
        id: DiagnosticIds.PreferMemberInitializersExceptWhenVaryingInitializations,
        title: "Should not initialize in declaration due to diverging initializations in constructors",
        messageFormat: "Do not initialize a field in its declaration if you have diverging initializations in constructors. This is to prevent unnecessary allocations.",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field initialization should not occur when there are diverging initializations in constructos. This is to prevent unnecessary allocations.",
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor RuleShouldInitializeInDeclarationWhenNoInitializationPresent = new(
        id: DiagnosticIds.PreferMemberInitializersWhenNoInitializationPresent,
        title: "Should initialize in declaration when no initialization present",
        messageFormat: "Initialize the field in its declaration when no distint initializations will occur in constructors",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field initialization should occur in the declaration unless there are diverging initializations in constructors, or the field is a value type or nullable being initialized to the default.",
        helpLinkUri: HelpLinkUri);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(GeneralRule, RuleExceptionInitializeToNullOrZero, RuleExceptionShouldNotInitializeInDeclaration, RuleShouldInitializeInDeclarationWhenNoInitializationPresent);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        ClassDeclarationSyntax classSyntaxNode = (ClassDeclarationSyntax)context.Node;
        IDictionary<string, FieldInitializationInfo> fieldInitializationInfo = new Dictionary<string, FieldInitializationInfo>(StringComparer.Ordinal);

        // Check in every constructor if there are member initializer candidates
        foreach (ConstructorDeclarationSyntax constructor in classSyntaxNode.ChildNodes().OfType<ConstructorDeclarationSyntax>())
        {
            FindMemberInitializerCandidates(context, constructor, fieldInitializationInfo);
        }

        // Report diagnostics on field declarations
        ReportDiagnosticsOnFieldDeclarations(context, classSyntaxNode, fieldInitializationInfo);
    }

    private static void ReportDiagnosticsOnFieldDeclarations(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclarationSyntax, IDictionary<string, FieldInitializationInfo> fieldInitializationInfos)
    {
        foreach (FieldDeclarationSyntax field in classDeclarationSyntax.ChildNodes().OfType<FieldDeclarationSyntax>())
        {
            SeparatedSyntaxList<VariableDeclaratorSyntax> variables = field.Declaration.Variables;

            if (variables.Count != 1)
            {
                // We only support single variable declarations
                continue;
            }

            VariableDeclaratorSyntax variable = variables[0];
            EqualsValueClauseSyntax? initializer = variable.Initializer;
            bool isInitializerPresent = initializer is not null;

            if (!fieldInitializationInfos.TryGetValue(variable.Identifier.Text, out FieldInitializationInfo fieldInfo))
            {
                // Check and report fields that are initialized to null or zero. Structs are also checked for empty initializers.
                if (isInitializerPresent
                    && (IsFieldNullOrZeroWithInitializer(context, initializer!)
                    || IsStructInitializerEmpty(context, field.Declaration.Type, initializer!)))
                {
                    context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleExceptionInitializeToNullOrZero));
                }
                else if (!isInitializerPresent
                        && context.SemanticModel.GetDeclaredSymbol(variable, cancellationToken: context.CancellationToken) is IFieldSymbol fieldSymbol
                        && !IsZeroOrNullInitializableType(fieldSymbol))
                {
                    // An initializer is not present, so if the field is not a value type or nullable, the user should initialize it in the declaration
                    context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleShouldInitializeInDeclarationWhenNoInitializationPresent));
                }
            }
            else
            {
                if (isInitializerPresent
                    && fieldInfo.ShouldNotInitializeInDeclaration)
                {
                    // An initializer is present, but the field should not be initialized in the declaration based on the constructor analysis
                    // For example, if the field has diverging constructor initializations, we should not initialize it in the declaration
                    context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleExceptionShouldNotInitializeInDeclaration));
                }
                else if (!fieldInfo.ShouldNotInitializeInDeclaration)
                {
                    IList<ExpressionStatementSyntax> fieldInitializersInConstructors = fieldInfo.FieldInitializersInConstructors;

                    if (isInitializerPresent)
                    {
                        Diagnostic[] diagnotics = new Diagnostic[fieldInitializersInConstructors.Count];

                        for (int i = 0; i < fieldInitializersInConstructors.Count; i++)
                        {
                            // If the field is initialized in the declaration, but it diverges from the constructor initializations, we should flag the declaration
                            if (!((AssignmentExpressionSyntax)fieldInitializersInConstructors[i].Expression).Right.IsEquivalentTo(initializer!.Value))
                            {
                                context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleExceptionShouldNotInitializeInDeclaration));
                                return;
                            }

                            diagnotics[i] = fieldInitializersInConstructors[i].GetLocation().CreateDiagnostic(GeneralRule);
                        }

                        for (int i = 0; i < diagnotics.Length; i++)
                        {
                            context.ReportDiagnostic(diagnotics[i]);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < fieldInitializersInConstructors.Count; i++)
                        {
                            // Field should be initialized in the declaration since there are no diverging initializations in constructors.
                            // We flag all the assignments in the constructors as diagnostics
                            context.ReportDiagnostic(fieldInitializersInConstructors[i].GetLocation().CreateDiagnostic(GeneralRule));
                        }
                    }
                }
            }
        }
    }

    private static bool IsFieldNullOrZeroWithInitializer(SyntaxNodeAnalysisContext context, EqualsValueClauseSyntax initializer)
    {
        SyntaxKind initializerKind = initializer.Value.Kind();

        if (initializerKind == SyntaxKind.NullLiteralExpression)
        {
            return string.Equals(((LiteralExpressionSyntax)initializer.Value).Token.ValueText, "null", StringComparison.Ordinal);
        }

        if (initializerKind == SyntaxKind.NumericLiteralExpression)
        {
            Optional<object?> constantValue = context.SemanticModel.GetConstantValue(initializer.Value, context.CancellationToken);
            return constantValue.HasValue && constantValue.Value is int intValue && intValue == 0;
        }

        return initializerKind == SyntaxKind.FalseLiteralExpression;
    }

    private static bool IsStructInitializerEmpty(SyntaxNodeAnalysisContext context, TypeSyntax typeSyntax, EqualsValueClauseSyntax initializer)
    {
        TypeInfo symbol = context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken);

        if (symbol.Type?.TypeKind != TypeKind.Struct
            || initializer.Value is not ObjectCreationExpressionSyntax objectCreation)
        {
            return false;
        }

        return objectCreation.ArgumentList?.Arguments.Count == 0;
    }

    private static void FindMemberInitializerCandidates(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor, IDictionary<string, FieldInitializationInfo> fields)
    {
        SeparatedSyntaxList<ParameterSyntax> arguments = constructor.ParameterList.Parameters;

        if (arguments.Count == 0)
        {
            // If the constructor has no arguments, we can consider all fields as candidates
            HandleEmptyArgumentsList(constructor, context, fields);
        }
        else
        {
            // If we can only add literal field assignments and invocation expressions that do not reference parameters
            // The analyzer does not support more complex scenarios such as whether the argument list contains something that depends on a parameter
            HandleArgumentsList(constructor, context, fields);
        }
    }

    private static void HandleEmptyArgumentsList(ConstructorDeclarationSyntax constructor, SyntaxNodeAnalysisContext context, IDictionary<string, FieldInitializationInfo> fields)
    {
        foreach (ExpressionStatementSyntax expressionStatement in constructor.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment
                && assignment.Left is IdentifierNameSyntax identifierName)
            {
                FieldInitializationInfo fieldInfo;

                if (!fields.TryGetValue(identifierName.Identifier.Text, out fieldInfo))
                {
                    if (context.SemanticModel.GetSymbolInfo(identifierName, cancellationToken: context.CancellationToken).Symbol is not IFieldSymbol fieldSymbol)
                    {
                        return;
                    }

                    fieldInfo = new FieldInitializationInfo(fieldSymbol.Name);
                    fields.Add(identifierName.Identifier.Text, fieldInfo);
                }

                if (fieldInfo.ShouldNotInitializeInDeclaration)
                {
                    return;
                }

                ProcessMemberInitializerCandidates(
                    assignment,
                    expressionStatement,
                    fieldInfo);
            }
        }
    }

    private static void HandleArgumentsList(ConstructorDeclarationSyntax constructor, SyntaxNodeAnalysisContext context, IDictionary<string, FieldInitializationInfo> fields)
    {
        foreach (ExpressionStatementSyntax expressionStatement in constructor.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment
                    && assignment.Left is IdentifierNameSyntax identifierName)
            {
                FieldInitializationInfo fieldInfo;

                if (!fields.TryGetValue(identifierName.Identifier.Text, out fieldInfo))
                {
                    if (context.SemanticModel.GetSymbolInfo(identifierName, cancellationToken: context.CancellationToken).Symbol is not IFieldSymbol fieldSymbol)
                    {
                        return;
                    }

                    fieldInfo = new FieldInitializationInfo(fieldSymbol.Name);
                    fields.Add(identifierName.Identifier.Text, fieldInfo);
                }

                if (fieldInfo.ShouldNotInitializeInDeclaration)
                {
                    return;
                }

                IOperation? operation = context.SemanticModel.GetOperation(assignment.Right, context.CancellationToken);

                if (operation is IParameterReferenceOperation || operation is ILocalReferenceOperation)
                {
                    return;
                }

                if (!IsConstructorParameterInUse(assignment.Right, context))
                {
                    ProcessMemberInitializerCandidates(
                        assignment,
                        expressionStatement,
                        fieldInfo);
                }
                else
                {
                    fieldInfo.ShouldNotInitializeInDeclaration = true;
                }
            }
        }
    }

    private static bool IsConstructorParameterInUse(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        return expression switch
        {
            InvocationExpressionSyntax invocationExpressionSyntax => IsConstructorParameterInUse(invocationExpressionSyntax.ArgumentList, context),
            ObjectCreationExpressionSyntax objectCreationExpressionSyntax => IsConstructorParameterInUse(objectCreationExpressionSyntax.ArgumentList, context),
            ImplicitObjectCreationExpressionSyntax implicitObjectCreationExpressionSyntax => IsConstructorParameterInUse(implicitObjectCreationExpressionSyntax.ArgumentList, context),
            InitializerExpressionSyntax initializerExpressionSyntax => IsConstructorParameterInUse(initializerExpressionSyntax.Expressions, context),
            _ => false,
        };
    }

    private static bool IsConstructorParameterInUse(SyntaxNode? argumentList, SyntaxNodeAnalysisContext context)
    {
        if (argumentList is null)
        {
            return false;
        }

        foreach (IdentifierNameSyntax identifierNameSyntax in argumentList.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            IOperation? operation = context.SemanticModel.GetOperation(identifierNameSyntax, context.CancellationToken);

            if (operation is IParameterReferenceOperation || operation is ILocalReferenceOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConstructorParameterInUse(SeparatedSyntaxList<ExpressionSyntax> expressions, SyntaxNodeAnalysisContext context)
    {
        foreach (IdentifierNameSyntax identifierNameSyntax in expressions.OfType<IdentifierNameSyntax>())
        {
            IOperation? operation = context.SemanticModel.GetOperation(identifierNameSyntax, context.CancellationToken);

            if (operation is IParameterReferenceOperation || operation is ILocalReferenceOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static void ProcessMemberInitializerCandidates(
        AssignmentExpressionSyntax assignment,
        ExpressionStatementSyntax expressionStatement,
        FieldInitializationInfo fieldInitializationInfo)
    {
        IList<ExpressionStatementSyntax> memberInitializersInConstructor = fieldInitializationInfo.FieldInitializersInConstructors;

        if (memberInitializersInConstructor.Count == 0)
        {
            memberInitializersInConstructor.Add(expressionStatement);
        }
        else if (((AssignmentExpressionSyntax)memberInitializersInConstructor[0].Expression).Right.IsEquivalentTo(assignment.Right))
        {
            memberInitializersInConstructor.Add(expressionStatement);
        }
        else
        {
            fieldInitializationInfo.ShouldNotInitializeInDeclaration = true;
        }
    }

    private static bool IsZeroOrNullInitializableType(IFieldSymbol fieldSymbol)
    {
        return fieldSymbol.Type.IsValueType || fieldSymbol.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private sealed record FieldInitializationInfo
    {
        public FieldInitializationInfo(
            string name)
        {
            FieldName = name;
            FieldInitializersInConstructors = new List<ExpressionStatementSyntax>();
        }

        public string FieldName { get; init; }

        public IList<ExpressionStatementSyntax> FieldInitializersInConstructors { get; init; }

        public bool ShouldNotInitializeInDeclaration { get; set; }
    }
}

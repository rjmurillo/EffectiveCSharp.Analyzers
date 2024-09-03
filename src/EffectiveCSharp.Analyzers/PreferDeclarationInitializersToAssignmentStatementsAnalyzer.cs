namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer" /> for Effective C# Item #12 - Prefer field declaration initializers.
/// </summary>
/// <seealso cref="DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferDeclarationInitializersToAssignmentStatementsAnalyzer : DiagnosticAnalyzer
{
    private static readonly string HelpLinkUri = $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticIds.PreferDeclarationInitializersToAssignmentStatement}.md";

    private static readonly DiagnosticDescriptor GeneralRule = new(
        id: DiagnosticIds.PreferDeclarationInitializersToAssignmentStatement,
        title: "Prefer field declaration initializers to assignment statements",
        messageFormat: "Use a field declaration initializer instead of an assignment statement",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Field initialization in a constructor that does not use an argument should be done with a field declaration initializer.",
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor RuleExceptionShouldNotInitializeToNullOrZero = new(
        id: DiagnosticIds.PreferDeclarationInitializersExceptNullOrZero,
        title: "Should not initialize to null or zero",
        messageFormat: "Do not initialize to null or zero as these already occur by default",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Field initialization to null or zero is redundant and should be avoided.",
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor RuleExceptionShouldNotInitializeInDeclaration = new(
        id: DiagnosticIds.PreferDeclarationInitializersExceptWhenVaryingInitializations,
        title: "Should not initialize in declaration due to diverging initializations in constructors",
        messageFormat: "Do not initialize a field in its declaration if you have diverging initializations in constructors. This is to prevent unnecessary allocations.",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Field initialization should not occur when there are diverging initializations in constructos. This is to prevent unnecessary allocations.",
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor RuleShouldInitializeInDeclarationWhenNoInitializationPresent = new(
        id: DiagnosticIds.PreferDeclarationInitializersWhenNoInitializationPresent,
        title: "Should initialize in declaration when no initialization present",
        messageFormat: "Initialize the field in its declaration when no distint initializations will occur in constructors",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Field initialization should occur in the declaration unless there are diverging initializations in constructors, or the field is a value type or nullable being initialized to the default.",
        helpLinkUri: HelpLinkUri);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(GeneralRule, RuleExceptionShouldNotInitializeToNullOrZero, RuleExceptionShouldNotInitializeInDeclaration, RuleShouldInitializeInDeclarationWhenNoInitializationPresent);

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

        // In order to keep track of the fields that are initialized in the constructors
        // We create this dictionary that will track a field's initialization in the constructors.
        // To save on time and memory, we only track fields that are initialized in the constructors.
        Dictionary<string, FieldInitializationInfo> fieldInitializationInfo = new(5, StringComparer.Ordinal);

        IEnumerable<SyntaxNode> childNodes = classSyntaxNode.ChildNodes();
        List<FieldDeclarationSyntax> fields = new(5);

        // Check in every constructor if there are field initializer candidates
        foreach (SyntaxNode node in childNodes)
        {
            switch (node)
            {
                case ConstructorDeclarationSyntax constructor:
                    IEnumerable<SyntaxNode>? nodes = constructor.Body?.ChildNodes();

                    if (nodes is null)
                    {
                        continue;
                    }

                    bool isConstructorParameterless = constructor.ParameterList.Parameters.Count == 0;

                    FindFieldInitializerCandidatesInScope(context, nodes, fieldInitializationInfo, isConstructorParameterless);
                    break;
                case PropertyDeclarationSyntax propertyDeclarationSyntax:
                    FindFieldInitializerCandidatesInPropertyDeclaration(propertyDeclarationSyntax, fieldInitializationInfo);
                    break;
                case FieldDeclarationSyntax fieldDeclarationSyntax:
                    fields.Add(fieldDeclarationSyntax);
                    break;
            }
        }

        // Report diagnostics on field declarations
        ReportDiagnosticsOnFieldDeclarations(context, fields, fieldInitializationInfo);
    }

    /// <summary>
    /// This method finds field initializer candidates in property declarations.
    /// This should not be as common as in constructors, but it is still a valid scenario.
    /// </summary>
    /// <param name="propertyDeclaration">The property declaration syntax to analyze.</param>
    /// <param name="fields">A dictionary tracking all fields intializations in constructors.</param>
    private static void FindFieldInitializerCandidatesInPropertyDeclaration(PropertyDeclarationSyntax propertyDeclaration, Dictionary<string, FieldInitializationInfo> fields)
    {
        SyntaxList<AccessorDeclarationSyntax>? accessors = propertyDeclaration.AccessorList?.Accessors;

        if (accessors is null)
        {
            return;
        }

        AccessorDeclarationSyntax? setter = null;

        for (int i = 0; i < accessors.Value.Count; ++i)
        {
            if (accessors.Value[i].Kind() == SyntaxKind.SetAccessorDeclaration)
            {
                setter = accessors.Value[i];
                break;
            }
        }

        if (setter is null)
        {
            return;
        }

        foreach (IdentifierNameSyntax identifierNameSyntax in setter.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            string fieldName = identifierNameSyntax.Identifier.Text;
            if (!fields.TryGetValue(fieldName, out FieldInitializationInfo fieldInfo))
            {
                fieldInfo = new FieldInitializationInfo(fieldName);
                fields.Add(fieldName, fieldInfo);
            }

            // We assume hat if you use a field in a setter, you are initializing it in some form,
            // and therefore we should initialize in the declaration.
            fieldInfo.ShouldNotInitializeInDeclaration = true;
        }
    }

    /// <summary>
    /// This method finds every instance of a field being initialized in a constructor.
    /// The initialization and some other information is then tracked via a <see cref="FieldInitializationInfo"/>.
    /// </summary>
    /// <param name="context">The node analysis context.</param>
    /// <param name="nodes">The nodes to analyze for field initializer candidates.</param>
    /// <param name="fields">A dictionary tracking all fields initialization in constructors.</param>
    /// <param name="isConstructorParameterless">A bool on whether the constructor has parameters.</param>
    /// <param name="isScopeConstructor">A bool on whether the scope the expressions are in is a constructor or some scope within the constructor, such as an if statement.</param>
    private static void FindFieldInitializerCandidatesInScope(SyntaxNodeAnalysisContext context, IEnumerable<SyntaxNode> nodes, Dictionary<string, FieldInitializationInfo> fields, bool isConstructorParameterless, bool isScopeConstructor = true)
    {
        foreach (SyntaxNode node in nodes)
        {
            switch (node)
            {
                case IfStatementSyntax ifStatementSyntax:
                    FindFieldInitializerCandidatesInScope(context, ifStatementSyntax.Statement.ChildNodes(), fields, isConstructorParameterless, false);

                    if (ifStatementSyntax.Else is not null)
                    {
                        FindFieldInitializerCandidatesInScope(context, ifStatementSyntax.Else.ChildNodes(), fields, isConstructorParameterless, false);
                    }

                    break;

                case ExpressionStatementSyntax expressionSyntax:
                    if (expressionSyntax.Expression is AssignmentExpressionSyntax assignment)
                    {
                        HandleConstructorExpressions(expressionSyntax, assignment, context, fields, isConstructorParameterless, isScopeConstructor);
                    }

                    break;
                case InvocationExpressionSyntax invocationExpressionSyntax:
                    HandleInvocation(invocationExpressionSyntax, context, fields);
                    break;
            }
        }
    }

    /// <summary>
    /// This method handles an expression in a constructor to find field initializer candidates.
    /// </summary>
    /// <param name="expressionStatementSyntax">The expression where the candidate assignment was found.</param>
    /// <param name="assignment">The assignment to check if for field initializations.</param>
    /// <param name="context">The node analysis context.</param>
    /// <param name="fields">A dictionary tracking all fields initializations in constructors.</param>
    /// <param name="isConstructorParameterless">A bool on whether the constructor has parameters.</param>
    /// <param name="isScopeConstructor">A bool on whether the scope the expressions are in is a constructor or some scope within the constructor, such as an if statement.</param>
    private static void HandleConstructorExpressions(
        ExpressionStatementSyntax expressionStatementSyntax,
        AssignmentExpressionSyntax assignment,
        SyntaxNodeAnalysisContext context,
        Dictionary<string, FieldInitializationInfo> fields,
        bool isConstructorParameterless,
        bool isScopeConstructor)
    {
        IdentifierNameSyntax? identifierName = assignment.Left switch
        {
            IdentifierNameSyntax idName => idName,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax idName } => idName,
            _ => null,
        };

        if (identifierName is null)
        {
            return;
        }

        // We assume the identifier is a field.
        // Calls to semantic model are super expensive, so we want to avoid them as much as possible.
        string fieldName = identifierName.Identifier.Text;
        if (!fields.TryGetValue(fieldName, out FieldInitializationInfo fieldInfo))
        {
            fieldInfo = new FieldInitializationInfo(fieldName);
            fields.Add(fieldName, fieldInfo);
            goto FIELD_SHOULD_BE_CHECKED;
        }

        if (fieldInfo.ShouldNotInitializeInDeclaration)
        {
            return;
        }

        FIELD_SHOULD_BE_CHECKED:
        if (!isScopeConstructor)
        {
            fieldInfo.ShouldNotInitializeInDeclaration = true;
            return;
        }

        ExpressionSyntax rightSide = assignment.Right;

        if ((isConstructorParameterless && rightSide is IdentifierNameSyntax)
                || rightSide is LiteralExpressionSyntax
                    or MemberAccessExpressionSyntax { Expression: PredefinedTypeSyntax })
        {
            goto PROCESS_FIELD_CANDIDATE;
        }

        if (rightSide is IdentifierNameSyntax
            || !IsExpressionValidDeclarationInitialization(rightSide, context, isConstructorParameterless))
        {
            fieldInfo.ShouldNotInitializeInDeclaration = true;
            return;
        }

        PROCESS_FIELD_CANDIDATE:
        ProcessFieldInitializerCandidate(
            assignment,
            expressionStatementSyntax,
            fieldInfo);
    }

    /// <summary>
    /// Checks whether the constructor parameter is in use in the expression.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="context">The analysis context.</param>
    /// <param name="isConstructorParameterLess">A bool on whether the constructor has parameters.</param>
    /// <returns>A bool, on whether we've found a constructor parameter in use.</returns>
    private static bool IsExpressionValidDeclarationInitialization(ExpressionSyntax expression, SyntaxNodeAnalysisContext context, bool isConstructorParameterLess)
    {
        return expression switch
        {
            // There are many ways of initializing the same field, so we need to check many different types of expressions.
            // For example, new List<string>() == [] == new List<string> { }. So we need to consider these scenarios.
            LiteralExpressionSyntax => true,
            IdentifierNameSyntax => isConstructorParameterLess,
            InvocationExpressionSyntax invocationExpressionSyntax => IsExpressionValidDeclarationInitialization(invocationExpressionSyntax, context, isConstructorParameterLess),
            ObjectCreationExpressionSyntax objectCreationExpressionSyntax => IsExpressionValidDeclarationInitialization(objectCreationExpressionSyntax.ArgumentList, context, isConstructorParameterLess),
            ImplicitObjectCreationExpressionSyntax implicitObjectCreationExpressionSyntax => IsExpressionValidDeclarationInitialization(implicitObjectCreationExpressionSyntax.ArgumentList, context, isConstructorParameterLess),
            InitializerExpressionSyntax initializerExpressionSyntax => IsExpressionValidDeclarationInitialization(initializerExpressionSyntax.Expressions, context, isConstructorParameterLess),
            BinaryExpressionSyntax binaryExpressionSyntax => IsExpressionValidDeclarationInitialization(binaryExpressionSyntax.Left, context, isConstructorParameterLess) && IsExpressionValidDeclarationInitialization(binaryExpressionSyntax.Right, context, isConstructorParameterLess),
            _ => false,
        };
    }

    private static bool IsExpressionValidDeclarationInitialization(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context, bool isConstructorParameterless)
    {
        return isConstructorParameterless && IsExpressionValidDeclarationInitialization(invocation.ArgumentList, context, isConstructorParameterless) &&
               IsStaticMethodInvocation(invocation, context);
    }

    // Overload for SyntaxNode
    private static bool IsExpressionValidDeclarationInitialization(ArgumentListSyntax? argumentList, SyntaxNodeAnalysisContext context, bool isConstructorParameterless)
    {
        if (argumentList is null)
        {
            return false;
        }

        foreach (SyntaxNode node in argumentList.ChildNodes())
        {
            if (node is not ArgumentSyntax argument)
            {
                continue;
            }

            if (argument.Expression is IdentifierNameSyntax
                || !IsExpressionValidDeclarationInitialization(argument.Expression, context, isConstructorParameterless))
            {
                return false;
            }
        }

        return true;
    }

    // Overload for SeparatedSyntaxList<ExpressionSyntax>
    private static bool IsExpressionValidDeclarationInitialization(SeparatedSyntaxList<ExpressionSyntax> expressions, SyntaxNodeAnalysisContext context, bool isConstructorParameterLess)
    {
        foreach (ExpressionSyntax expression in expressions)
        {
            if (!IsExpressionValidDeclarationInitialization(expression, context, isConstructorParameterLess))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether the invocation expression is a static method invocation.
    /// </summary>
    /// <param name="invocationExpression">The target <see cref="InvocationExpressionSyntax"/>.</param>
    /// <param name="context">The <see cref="SyntaxNodeAnalysisContext"/>.</param>
    /// <returns>A bool on whether the invocation is for a static method.</returns>
    private static bool IsStaticMethodInvocation(InvocationExpressionSyntax invocationExpression, SyntaxNodeAnalysisContext context)
    {
        // Get the symbol information for the method being invoked
        IMethodSymbol? methodSymbol = (IMethodSymbol?)context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken).Symbol;

        // Check if the method symbol is not null and is not static
        return methodSymbol?.IsStatic is true;
    }

    /// <summary>
    /// Processes a field initializer candidate.
    /// If the initialization is not being tracked or is identical to the first tracked initialization,
    /// it is added to the list of initializers.
    /// Else, the field is marked as not to be initialized in the declaration since there are diverging initializations.
    /// </summary>
    /// <param name="assignment">The assignment node to check for equivalence.</param>
    /// <param name="expressionStatement">The expression statement to cache if needed.</param>
    /// <param name="fieldInitializationInfo">The field intialization info object tracking the field's initializers' state.</param>
    private static void ProcessFieldInitializerCandidate(
        AssignmentExpressionSyntax assignment,
        ExpressionStatementSyntax expressionStatement,
        FieldInitializationInfo fieldInitializationInfo)
    {
        List<ExpressionStatementSyntax> fieldInitializersInConstructors = fieldInitializationInfo.FieldInitializersInConstructors;

        if (fieldInitializersInConstructors.Count == 0)
        {
            fieldInitializersInConstructors.Add(expressionStatement);
        }
        else if (((AssignmentExpressionSyntax)fieldInitializersInConstructors[0].Expression).Right.IsEquivalentTo(assignment.Right))
        {
            fieldInitializersInConstructors.Add(expressionStatement);
        }
        else
        {
            fieldInitializationInfo.ShouldNotInitializeInDeclaration = true;
        }
    }

    /// <summary>
    /// This method handles invocations in constructors that are not part of an assignment expression.
    /// </summary>
    /// <param name="invocation">The invocation expression to check for field initialization candidates.</param>
    /// <param name="context">The node analysis context.</param>
    /// <param name="fields">A dictionary tracking all fields initialization in constructors.</param>
    private static void HandleInvocation(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context, Dictionary<string, FieldInitializationInfo> fields, bool isConstructorParameterless, bool isScopeConstructor)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol
            || methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax methodDeclaration)
        {
            return;
        }

        if (methodDeclaration.Body is null)
        {
            return;
        }

        FindFieldInitializerCandidatesInScope(context, methodDeclaration.Body.ChildNodes(), fields, isConstructorParameterless, isScopeConstructor);
    }

    /// <summary>
    /// Reports diagnostics on field declarations.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="classDeclarationChildNodes">The class declaration child nodes to iterate through.</param>
    /// <param name="fieldInitializationInfos">The initalization info for all fields present in a constructor.</param>
    private static void ReportDiagnosticsOnFieldDeclarations(SyntaxNodeAnalysisContext context, List<FieldDeclarationSyntax> classDeclarationChildNodes, Dictionary<string, FieldInitializationInfo> fieldInitializationInfos)
    {
        for (int i = 0; i < classDeclarationChildNodes.Count; ++i)
        {
            FieldDeclarationSyntax field = classDeclarationChildNodes[i];

            SeparatedSyntaxList<VariableDeclaratorSyntax> variables = field.Declaration.Variables;

            if (variables.Count != 1)
            {
                // We only support single variable declarations for now.
                continue;
            }

            VariableDeclaratorSyntax variable = variables[0];
            EqualsValueClauseSyntax? initializer = variable.Initializer;
            bool isInitializerPresent = initializer is not null;

            if (!fieldInitializationInfos.TryGetValue(variable.Identifier.Text, out FieldInitializationInfo fieldInfo))
            {
                // Field was not found in constructors.
                HandleFieldNotInConstructors(context, field, variable, initializer, isInitializerPresent);
            }
            else
            {
                // Field was found in constructors.
                HandleFieldsInConstructors(context, field, initializer!, isInitializerPresent, fieldInfo);
            }
        }
    }

    /// <summary>
    /// Handles fields that are not initialized in constructors.
    /// If the initializer is present, and the field is a value type that is initialied to null, zero, false or an empty struct,
    /// a diagnostic is reported to remove the initialization in the declaration.
    /// If the initializer is not present, and the field is not a value type or nullable, a diagnostic is reported to
    /// initialize in declaration.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="field">The field declaration to analyze.</param>
    /// <param name="variable">The variable declarator node.</param>
    /// <param name="initializer">The field initializer.</param>
    /// <param name="isInitializerPresent">A bool on whether the initializer already exists or not.</param>
    private static void HandleFieldNotInConstructors(
        SyntaxNodeAnalysisContext context,
        FieldDeclarationSyntax field,
        VariableDeclaratorSyntax variable,
        EqualsValueClauseSyntax? initializer,
        bool isInitializerPresent)
    {
        if (isInitializerPresent
            && (IsFieldNullZeroOrFalseWithInitializer(context, initializer!)
            || IsStructInitializerEmpty(context, field.Declaration.Type, initializer!)))
        {
            // If the initializer is present, and the field is a value type that is initialied to null, zero, false or an empty struct,
            // a diagnostic is reported to remove the initialization in the declaration.
            context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleExceptionShouldNotInitializeToNullOrZero));
        }
        else if (!isInitializerPresent
            && context.SemanticModel.GetDeclaredSymbol(variable, cancellationToken: context.CancellationToken) is IFieldSymbol fieldSymbol
            && !IsZeroOrNullInitializableType(fieldSymbol))
        {
            // If the initializer is not present, and the field is not a value type or nullable, a diagnostic is reported to
            // initialize in declaration.
            context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleShouldInitializeInDeclarationWhenNoInitializationPresent));
        }
    }

    /// <summary>
    /// Determines whether the field is initialized to null, zero or false.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="initializer">The initializer node to check.</param>
    /// <returns>A bool on whether the field was initialized to null, zero or false.</returns>
    private static bool IsFieldNullZeroOrFalseWithInitializer(SyntaxNodeAnalysisContext context, EqualsValueClauseSyntax initializer)
    {
        SyntaxKind initializerKind = initializer.Value.Kind();

        if (initializerKind == SyntaxKind.NullLiteralExpression)
        {
            // To avoid calling the semanticmode, we simply check if the value text is "null".
            return string.Equals(((LiteralExpressionSyntax)initializer.Value).Token.ValueText, "null", StringComparison.Ordinal);
        }

        if (initializerKind == SyntaxKind.NumericLiteralExpression)
        {
            // There are many ways of intializing to zero (0, 0d, 0f, etc), so we use the semantic model to check the constant value
            // since that is 0 regardless of the syntax used.
            Optional<object?> constantValue = context.SemanticModel.GetConstantValue(initializer.Value, context.CancellationToken);
            return constantValue.HasValue && constantValue.Value is int intValue && intValue == 0;
        }

        // We check if the initializer is a false literal expression.
        return initializerKind == SyntaxKind.FalseLiteralExpression;
    }

    /// <summary>
    /// Determines whether the struct initializer is empty.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="typeSyntax">The type node of the field.</param>
    /// <param name="initializer">The initializer node to check.</param>
    /// <returns>A bool on whether the struct initializer is empty.</returns>
    private static bool IsStructInitializerEmpty(SyntaxNodeAnalysisContext context, TypeSyntax typeSyntax, EqualsValueClauseSyntax initializer)
    {
        TypeInfo symbol = context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken);

        // if the type is not a struct or the initializer is not an object creation (meaning the struct is being initialized)
        // we return false.
        if (symbol.Type?.TypeKind != TypeKind.Struct
            || initializer.Value is not ObjectCreationExpressionSyntax objectCreation)
        {
            return false;
        }

        // If the object creation has no arguments, we consider it an empty struct initializer.
        return objectCreation.ArgumentList?.Arguments.Count == 0;
    }

    /// <summary>
    /// Determines whether the field is a zero or null initializable type.
    /// </summary>
    /// <param name="fieldSymbol">The field symbol information to check.</param>
    /// <returns>A bool, on whether the field is a type that can be initialized to null or 0.</returns>
    private static bool IsZeroOrNullInitializableType(IFieldSymbol fieldSymbol)
    {
        return fieldSymbol.Type.IsValueType || fieldSymbol.NullableAnnotation == NullableAnnotation.Annotated;
    }

    /// <summary>
    /// Handles fields that are initialized in constructors.
    /// </summary>
    /// <param name="context">The <see cref="SyntaxNodeAnalysisContext"/>.</param>
    /// <param name="field">The <see cref="FieldDeclarationSyntax"/> to handle.</param>
    /// <param name="initializer">The <see cref="EqualsValueClauseSyntax"/> which contains the field initializer.</param>
    /// <param name="isInitializerPresent">A bool on whether an initializer is present in the field declaration.</param>
    /// <param name="fieldInfo">The field's initialization info found in the class's constructors.</param>
    private static void HandleFieldsInConstructors(
        SyntaxNodeAnalysisContext context,
        FieldDeclarationSyntax field,
        EqualsValueClauseSyntax initializer,
        bool isInitializerPresent,
        FieldInitializationInfo fieldInfo)
    {
        if (fieldInfo.ShouldNotInitializeInDeclaration)
        {
            if (isInitializerPresent)
            {
                // The field was marked as not to initialize in the declaration due to diverging initializations in constructors.
                // and an initializer is presen, meaning we report a diagnostic.ssssssssssssssss
                context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleExceptionShouldNotInitializeInDeclaration));
            }

            return;
        }

        IList<ExpressionStatementSyntax> fieldInitializersInConstructors = fieldInfo.FieldInitializersInConstructors;

        // If the field is initialized in the declaration and in the constructors,
        // we check if the initializations are diverging.
        // Since we already checked if the field should not initialize in the declaration,
        // we know all initializations in the constructors are the same.
        // So we only need to check if the initializer is diverging from the constructor initializations.
        if (isInitializerPresent
            && fieldInitializersInConstructors.Count != 0
            && HasDivergingInitializations(fieldInitializersInConstructors[0], initializer))
        {
            context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleExceptionShouldNotInitializeInDeclaration));
            return;
        }

        // We know the initializers in the constructors are the same, so we report diagnostics (general rule) on them.
        ReportDiagnosticsForInitializersInConstructors(context, fieldInitializersInConstructors);
    }

    /// <summary>
    /// Checks if the initializers in the constructors are diverging from the initializer in the declaration.
    /// </summary>
    /// <param name="initializerInConstructor">A <see cref="ExpressionStatementSyntax"/> from the constructor.</param>
    /// <param name="initializerInDeclaration">The initializer from the declaration.</param>
    /// <returns>A bool on whether the constructor initializers diverge from the declaration.</returns>
    private static bool HasDivergingInitializations(ExpressionStatementSyntax initializerInConstructor, EqualsValueClauseSyntax initializerInDeclaration)
    {
        return !((AssignmentExpressionSyntax)initializerInConstructor.Expression).Right.IsEquivalentTo(initializerInDeclaration.Value);
    }

    /// <summary>
    /// Reports diagnostics for initializers in constructors <see cref="GeneralRule"/>.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="initializers">The list of constructor field initializers.</param>
    private static void ReportDiagnosticsForInitializersInConstructors(SyntaxNodeAnalysisContext context, IList<ExpressionStatementSyntax> initializers)
    {
        for (int i = 0; i < initializers.Count; i++)
        {
            context.ReportDiagnostic(initializers[i].GetLocation().CreateDiagnostic(GeneralRule));
        }
    }

    /// <summary>
    /// A record to track field initialization information in constructors.
    /// </summary>
    private sealed class FieldInitializationInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FieldInitializationInfo"/> class.
        /// </summary>
        /// <param name="name">The field name.</param>
        public FieldInitializationInfo(
            string name)
        {
            FieldName = name;
            FieldInitializersInConstructors = new List<ExpressionStatementSyntax>(5);
        }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Gets the field initializers in constructors.
        /// </summary>
        public List<ExpressionStatementSyntax> FieldInitializersInConstructors { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the field should not initialize in the declaration.
        /// </summary>
        public bool ShouldNotInitializeInDeclaration { get; set; }
    }
}

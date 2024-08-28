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
        Dictionary<string, FieldInitializationInfo> fieldInitializationInfo = new(StringComparer.Ordinal);

        IEnumerable<SyntaxNode> childNodes = classSyntaxNode.ChildNodes();

        // Check in every constructor if there are field initializer candidates
        foreach (ConstructorDeclarationSyntax constructor in childNodes.OfType<ConstructorDeclarationSyntax>())
        {
            FindFieldInitializerCandidatesInConstructors(context, constructor, fieldInitializationInfo);
        }

        // Check in every property declaration if there are field initializer candidates
        foreach (PropertyDeclarationSyntax propertyDeclarationSyntax in childNodes.OfType<PropertyDeclarationSyntax>())
        {
            FindFieldInitializerCandidatesInPropertyDeclaration(propertyDeclarationSyntax, fieldInitializationInfo);
        }

        // Report diagnostics on field declarations
        ReportDiagnosticsOnFieldDeclarations(context, classSyntaxNode, fieldInitializationInfo);
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
    /// <param name="constructor">The constructor declaration to search fields for.</param>
    /// <param name="fields">A dictionary tracking all fields intializations in constructors.</param>
    private static void FindFieldInitializerCandidatesInConstructors(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor, Dictionary<string, FieldInitializationInfo> fields)
    {
        SeparatedSyntaxList<ParameterSyntax> arguments = constructor.ParameterList.Parameters;
        IEnumerable<SyntaxNode> descendantNodes = constructor.DescendantNodes();

        HandleArgumentsList(descendantNodes, context, fields, checkConstructorParameters: arguments.Count != 0);
        HandleInvocations(descendantNodes, context, fields);
    }

    /// <summary>
    /// This method handles assignment expression in constructors to find field initializer candidates.
    /// </summary>
    /// <param name="descendantNodes">The constructor's descendant nodes.</param>
    /// <param name="context">The node analysis context.</param>
    /// <param name="fields">A dictionary tracking all fields intializations in constructors.</param>
    /// <param name="checkConstructorParameters">A bool on whether to check constructor parameters for field initializations.</param>
    private static void HandleArgumentsList(
        IEnumerable<SyntaxNode> descendantNodes,
        SyntaxNodeAnalysisContext context,
        Dictionary<string, FieldInitializationInfo> fields,
        bool checkConstructorParameters)
    {
        foreach (AssignmentExpressionSyntax assignment in descendantNodes.OfType<AssignmentExpressionSyntax>())
        {
            bool isFieldAssignment = assignment.Left is IdentifierNameSyntax;

            // We want to ignore assignments that are not part of an expression statement or are not an identifier
            if (assignment.Parent is not ExpressionStatementSyntax
                || !(assignment.Left is MemberAccessExpressionSyntax
                || isFieldAssignment))
            {
                continue;
            }

            IdentifierNameSyntax identifierName = isFieldAssignment ? (IdentifierNameSyntax)assignment.Left : (IdentifierNameSyntax)((MemberAccessExpressionSyntax)assignment.Left).Name;

            // We assume the identifier is a field. Calls to semantic model are super expensive, so we want to avoid them as much as possible.
            string fieldName = identifierName.Identifier.Text;
            if (!fields.TryGetValue(fieldName, out FieldInitializationInfo fieldInfo))
            {
                fieldInfo = new FieldInitializationInfo(fieldName);
                fields.Add(fieldName, fieldInfo);
            }

            if (fieldInfo.ShouldNotInitializeInDeclaration)
            {
                continue;
            }

            if (assignment.Right is InvocationExpressionSyntax invocationExpressionSyntax
                && !IsStaticMethodInvocation(invocationExpressionSyntax, context))
            {
                // If the assignment is to a non-static method invocation, we can ignore it.
                fieldInfo.ShouldNotInitializeInDeclaration = true;
                continue;
            }

            if (checkConstructorParameters)
            {
                IOperation? operation = context.SemanticModel.GetOperation(assignment.Right, context.CancellationToken);

                // ILocalReferenceOperation: We do not yet have logic to verify if it is related to a constructor parameter, so we assume it is.
                if (operation is IParameterReferenceOperation
                    || operation is ILocalReferenceOperation
                    || IsConstructorParameterInUse(assignment.Right, context))
                {
                    fieldInfo.ShouldNotInitializeInDeclaration = true;
                    continue;
                }
            }

            ProcessFieldInitializerCandidate(
                assignment,
                (ExpressionStatementSyntax)assignment.Parent!,
                fieldInfo);
        }
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
    /// Checks whether the constructor parameter is in use in the expression.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>A bool, on whether we've found a constructor parameter in use.</returns>
    private static bool IsConstructorParameterInUse(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        return expression switch
        {
            // There are many ways of initializing the same field, so we need to check many different types of expressions.
            // For example, new List<string>() == [] == new List<string> { }. So we need to consider these scenarios.
            InvocationExpressionSyntax invocationExpressionSyntax => IsConstructorParameterInUse(invocationExpressionSyntax.ArgumentList, context),
            ObjectCreationExpressionSyntax objectCreationExpressionSyntax => IsConstructorParameterInUse(objectCreationExpressionSyntax.ArgumentList, context),
            ImplicitObjectCreationExpressionSyntax implicitObjectCreationExpressionSyntax => IsConstructorParameterInUse(implicitObjectCreationExpressionSyntax.ArgumentList, context),
            InitializerExpressionSyntax initializerExpressionSyntax => IsConstructorParameterInUse(initializerExpressionSyntax.Expressions, context),
            _ => false,
        };
    }

    // Overload for SyntaxNode
    private static bool IsConstructorParameterInUse(SyntaxNode? argumentList, SyntaxNodeAnalysisContext context)
    {
        return argumentList is not null && AreIdentifierNodesReferencingConstructorParameters(argumentList.DescendantNodes().OfType<IdentifierNameSyntax>(), context);
    }

    // Overload for SeparatedSyntaxList<ExpressionSyntax>
    private static bool IsConstructorParameterInUse(SeparatedSyntaxList<ExpressionSyntax> expressions, SyntaxNodeAnalysisContext context)
    {
        return AreIdentifierNodesReferencingConstructorParameters(expressions.OfType<IdentifierNameSyntax>(), context);
    }

    /// <summary>
    /// Checks whether the constructor parameter is in use in the given nodes.
    /// </summary>
    /// <param name="identifiers">The <see cref="IEnumerable{IdentifierNameSyntax}"/> to check for constructor parameter usage.</param>
    /// <param name="context">The analysis context.</param>
    /// <returns>A bool, on whether a constructor parameter was found in the nodes to check.</returns>
    private static bool AreIdentifierNodesReferencingConstructorParameters(IEnumerable<IdentifierNameSyntax> identifiers, SyntaxNodeAnalysisContext context)
    {
        foreach (IdentifierNameSyntax identifierNameSyntax in identifiers)
        {
            IOperation? operation = context.SemanticModel.GetOperation(identifierNameSyntax, context.CancellationToken);

            // If the operation is a parameter reference, we know that the constructor parameter is in use.
            // If the operation is a local reference, we know that the local variable is in use, and
            // we do not have logic yet to verify if it is related to a constructor parameter, so we assume it is.
            if (operation is IParameterReferenceOperation || operation is ILocalReferenceOperation)
            {
                return true;
            }
        }

        return false;
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
        IList<ExpressionStatementSyntax> fieldInitializersInConstructors = fieldInitializationInfo.FieldInitializersInConstructors;

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
    /// <param name="descendantNodes">The constructor's descendant nodes.</param>
    /// <param name="context">The node analysis context.</param>
    /// <param name="fields">A dictionary tracking all fields intializations in constructors.</param>
    private static void HandleInvocations(IEnumerable<SyntaxNode> descendantNodes, SyntaxNodeAnalysisContext context, Dictionary<string, FieldInitializationInfo> fields)
    {
        Stack<ExpressionStatementSyntax> nodeStack = new();

        foreach (InvocationExpressionSyntax invocation in descendantNodes.OfType<InvocationExpressionSyntax>())
        {
            // If the invocation is part of an assignment expression, we ignore it
            // since we are only interested in invocations that are not part of an assignment.
            if (invocation.Ancestors().OfType<AssignmentExpressionSyntax>().Any()
                || !TryGetMethodDeclaration(invocation, context, out MethodDeclarationSyntax? methodDeclaration))
            {
                continue;
            }

            PushExpressionStatements(methodDeclaration!, nodeStack);

            // We DFS through the method to find all invocations that are not part of an assignment expression.
            // This includes searching method declarations invoked by the method.
            while (nodeStack.Count > 0)
            {
                ExpressionStatementSyntax node = nodeStack.Pop();
                ProcessExpressionStatementFromMethod(node, context, fields, nodeStack);
            }
        }
    }

    /// <summary>
    /// Tries to get the method declaration from an invocation expression.
    /// </summary>
    /// <param name="invocation">The invocation expression to check for a method.</param>
    /// <param name="context">The syntax analysis context.</param>
    /// <param name="methodDeclaration">The output <see cref="MethodDeclarationSyntax"/>, if found.</param>
    /// <returns>A bool on whether we sucessfully found a method declaration from <paramref name="invocation"/>.</returns>
    private static bool TryGetMethodDeclaration(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context, out MethodDeclarationSyntax? methodDeclaration)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol
            || methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax methodDeclarationFound)
        {
            methodDeclaration = null;
            return false;
        }

        methodDeclaration = methodDeclarationFound;
        return true;
    }

    /// <summary>
    /// Pushes all expression statements in a method declaration to a stack.
    /// </summary>
    /// <param name="methodDeclaration">The <see cref="MethodDeclarationSyntax"/> to find <see cref="ExpressionStatementSyntax"/> descendants.</param>
    /// <param name="nodeStack">The <see cref="Stack{T}"/> to push <see cref="ExpressionStatementSyntax"/> nodes.</param>
    private static void PushExpressionStatements(MethodDeclarationSyntax methodDeclaration, Stack<ExpressionStatementSyntax> nodeStack)
    {
        foreach (ExpressionStatementSyntax expressionStatement in methodDeclaration.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            nodeStack.Push(expressionStatement);
        }
    }

    /// <summary>
    /// Processes an expression statement from a method declaration.
    /// If the expression is an invocation, it retrieves the method declaration and pushes its expression statements onto the stack.
    /// If the expression is an assignment to a field, it marks the field as initialized to avoid initialization in the declaration.
    /// It ignores all other expressions.
    /// </summary>
    /// <param name="node">The expression statement to process.</param>
    /// <param name="context">The context for the syntax node analysis.</param>
    /// <param name="fields">A dictionary of field initialization information.</param>
    /// <param name="nodeStack">A stack to hold expression statements for further processing.</param>
    private static void ProcessExpressionStatementFromMethod(ExpressionStatementSyntax node, SyntaxNodeAnalysisContext context, Dictionary<string, FieldInitializationInfo> fields, Stack<ExpressionStatementSyntax> nodeStack)
    {
        if (node.Expression is InvocationExpressionSyntax invocationExpressionSyntax)
        {
            if (TryGetMethodDeclaration(invocationExpressionSyntax, context, out MethodDeclarationSyntax? nestedMethodDeclaration))
            {
                // We found an invocation expression to a method declaration, so we push the expression statements in the method to the stack.
                PushExpressionStatements(nestedMethodDeclaration!, nodeStack);
            }
        }
        else if (node.Expression is AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifierNameSyntax })
        {
            // We found a field assignment inside this method, so we assume the field is being initialized,
            // and we should not initialize in the declaration.
            string fieldName = identifierNameSyntax.Identifier.Text;
            if (!fields.TryGetValue(fieldName, out FieldInitializationInfo fieldInfo))
            {
                fieldInfo = new FieldInitializationInfo(fieldName);
                fields.Add(fieldName, fieldInfo);
            }

            fieldInfo.ShouldNotInitializeInDeclaration = true;
        }
    }

    /// <summary>
    /// Reports diagnostics on field declarations.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="classDeclarationSyntax">The class declaration node to find the fields in.</param>
    /// <param name="fieldInitializationInfos">The initalization info for all fields present in a constructor.</param>
    private static void ReportDiagnosticsOnFieldDeclarations(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclarationSyntax, Dictionary<string, FieldInitializationInfo> fieldInitializationInfos)
    {
        foreach (FieldDeclarationSyntax field in classDeclarationSyntax.ChildNodes().OfType<FieldDeclarationSyntax>())
        {
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
    private sealed record FieldInitializationInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FieldInitializationInfo"/> class.
        /// </summary>
        /// <param name="name">The field name.</param>
        public FieldInitializationInfo(
            string name)
        {
            FieldName = name;
            FieldInitializersInConstructors = new List<ExpressionStatementSyntax>();
        }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Gets the field initializers in constructors.
        /// </summary>
        public IList<ExpressionStatementSyntax> FieldInitializersInConstructors { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the field should not initialize in the declaration.
        /// </summary>
        public bool ShouldNotInitializeInDeclaration { get; set; }
    }
}

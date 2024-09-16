namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #13 - Use Proper Initialization for Static Class Members
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StaticClassMemberInitializationAnalyzer : DiagnosticAnalyzer
{
    private static readonly string DiagnosticId = DiagnosticIds.StaticClassMemberInitialization;
    private static readonly LocalizableString Description = "Static fields requiring complex or potentially exception-throwing initialization should be initialized within a static constructor or using Lazy<T>.";
    private static readonly LocalizableString MessageFormat = "Static field '{0}' should be initialized in a static constructor or using Lazy<T>";
    private static readonly LocalizableString Title = "Use proper initialization for static class members";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Categories.Initialization,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticId}.md");

    private static readonly HashSet<string> SafeMethods = new(StringComparer.Ordinal)
    {
        "System.DateTime.Now",
        "System.Math.Abs",
        "System.Math.Max",
        "System.Math.Min",
        "System.Math.Sqrt",
        "System.Math.Pow",
        "System.Math.Ceiling",
        "System.Math.Floor",
        "System.Math.Round",
        "System.Math.Truncate",
        "System.Math.Log",
        "System.Math.Log10",
        "System.Math.Sin",
        "System.Math.Cos",
        "System.Math.Tan",
        "System.String.IsNullOrEmpty",
        "System.String.IsNullOrWhiteSpace",
        "System.Enum.GetValues",
        "System.BitConverter.ToInt32",
        "System.BitConverter.GetBytes",
        "System.TimeSpan.Zero",
        "System.TimeSpan.MaxValue",
        "System.TimeSpan.MinValue",
        "System.Guid.Empty",
        "System.Text.Encoding.UTF8",
        "System.Text.Encoding.UTF16",
        "System.Text.Encoding.UTF32",
        "System.Text.Encoding.ASCII",
    };

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // Ensure thread-safety and performance
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // Read configuration options
        context.RegisterCompilationStartAction(
            compilationContext =>
            {
                IEnumerable<string> additionalSafeMethods = GetConfiguredSafeMethods(compilationContext.Options);
                SafeMethods.UnionWith(additionalSafeMethods);

                // Register action to analyze field declarations
                compilationContext.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
            });
    }

    private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not FieldDeclarationSyntax fieldDeclaration)
        {
            return;
        }

        // Check for 'static' modifier
        if (!fieldDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }

        // Ignore constants
        if (fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword))
        {
            return;
        }

        // Iterate through variable declarations
        for (int i = 0; i < fieldDeclaration.Declaration.Variables.Count; i++)
        {
            VariableDeclaratorSyntax variable = fieldDeclaration.Declaration.Variables[i];

            // Check if there is an initializer
            if (variable.Initializer == null)
            {
                continue;
            }

            ExpressionSyntax initializer = variable.Initializer.Value;
            SemanticModel semanticModel = context.SemanticModel;

            // Determine if the initializer is complex or may throw exceptions
            if (!IsComplexInitializer(initializer, semanticModel))
            {
                continue;
            }

            // Report diagnostic
            Diagnostic diagnostic = variable.GetLocation().CreateDiagnostic(Rule, variable.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static List<string> GetConfiguredSafeMethods(AnalyzerOptions options)
    {
        List<string> safeMethods = new();
        AnalyzerConfigOptions configOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;

        if (!configOptions.TryGetValue($"dotnet_diagnostic.{DiagnosticId}.safe_methods", out string? methods))
        {
            return safeMethods;
        }

        string[] methodNames = methods.Split([','], StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < methodNames.Length; i++)
        {
            string methodName = methodNames[i];
            safeMethods.Add(methodName.Trim());
        }

        return safeMethods;
    }
    private static bool IsComplexInitializer(ExpressionSyntax initializer, SemanticModel semanticModel)
    {
        switch (initializer)
        {
            // Simple initializers are literals or simple binary expressions
            case LiteralExpressionSyntax:
            case PrefixUnaryExpressionSyntax:
            case PostfixUnaryExpressionSyntax:
                return false;

            case BinaryExpressionSyntax binaryExpr:
                // Check if both sides are literals or simple identifiers
                return !(IsSimpleExpression(binaryExpr.Left, semanticModel) && IsSimpleExpression(binaryExpr.Right, semanticModel));

            case InvocationExpressionSyntax invocationExpr:
                if (IsNameOfExpression(invocationExpr))
                {
                    return false; // Not complex
                }

                // Check if the method call is safe
                return !IsSafeMethodCall(invocationExpr, semanticModel);

            case ArrayCreationExpressionSyntax arrayCreationExpr:
                // Handle simple array creation
                return !IsSimpleArrayCreation(arrayCreationExpr, semanticModel);

            case ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpr:
                // Handle simple implicit array creation
                return !IsSimpleImplicitArrayCreation(implicitArrayCreationExpr, semanticModel);

            case ObjectCreationExpressionSyntax objectCreationExpr:
                // Handle collection initializations
                return !IsSimpleCollectionInitialization(objectCreationExpr, semanticModel);

            case ConditionalExpressionSyntax conditionalExpr:
                // Check if condition, whenTrue, and whenFalse are simple
                return !(IsSimpleExpression(conditionalExpr.Condition, semanticModel)
                         && IsSimpleExpression(conditionalExpr.WhenTrue, semanticModel)
                         && IsSimpleExpression(conditionalExpr.WhenFalse, semanticModel));

            default:
                // Check if the expression is a compile-time constant
                bool isConstant = semanticModel.IsCompileTimeConstant(initializer);

                if (isConstant)
                {
                    return false; // Not complex
                }

                // All other cases are considered complex
                return true;
        }
    }
    private static bool IsNameOfExpression(InvocationExpressionSyntax invocationExpr)
    {
        if (invocationExpr.Expression is IdentifierNameSyntax identifierName)
        {
            return string.Equals(identifierName.Identifier.Text, "nameof", StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsSafeField(IFieldSymbol fieldSymbol)
    {
        // Allow static fields from the System namespace
        if (fieldSymbol.IsStatic)
        {
            string containingNamespace = fieldSymbol.ContainingType.ContainingNamespace.ToDisplayString();
            if (containingNamespace.StartsWith("System", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSafeMemberAccess(MemberAccessExpressionSyntax memberAccessExpr, SemanticModel semanticModel)
    {
        // Get the symbol info for the member access expression
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(memberAccessExpr);
        ISymbol? symbol = symbolInfo.Symbol;

        return symbol switch
        {
            IPropertySymbol propertySymbol => IsSafeProperty(propertySymbol),
            IFieldSymbol fieldSymbol => IsSafeField(fieldSymbol),
            _ => false
        };
    }

    private static bool IsSafeMethodCall(InvocationExpressionSyntax invocationExpr, SemanticModel semanticModel)
    {
        // Get the symbol info for the invocation expression
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocationExpr);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return false; // Unable to determine method symbol; consider complex
        }

        INamedTypeSymbol? containingType = methodSymbol.ContainingType;
        if (containingType == null)
        {
            return false;
        }

        // Get the fully qualified method name and check against the allow list
        string methodFullName = methodSymbol.ContainingType.ToDisplayString() + "." + methodSymbol.Name;

        if (SafeMethods.Contains(methodFullName))
        {
            return true;
        }

        // Check if all arguments are compile-time constants
        for (int i = 0; i < invocationExpr.ArgumentList.Arguments.Count; i++)
        {
            ArgumentSyntax argument = invocationExpr.ArgumentList.Arguments[i];
            if (!semanticModel.IsCompileTimeConstant(argument.Expression))
            {
                return false; // Argument is not a compile-time constant
            }
        }

        return false;
    }

    private static bool IsSafeProperty(IPropertySymbol propertySymbol)
    {
        // Allow static properties from the System namespace
        if (propertySymbol.IsStatic)
        {
            string containingNamespace = propertySymbol.ContainingType.ContainingNamespace.ToDisplayString();
            if (containingNamespace.StartsWith("System", StringComparison.Ordinal))
            {
                return true;
            }
        }
        else
        {
            // For instance properties, check if the containing type is considered safe
            var containingTypeName = propertySymbol.ContainingType.ToDisplayString();

            // For example, DateTime.Now.Hour
            if (containingTypeName == "System.DateTime")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSimpleArrayCreation(ArrayCreationExpressionSyntax arrayCreationExpr, SemanticModel semanticModel)
    {
        // Check if the type is an array type
        if (arrayCreationExpr is not { Type: ArrayTypeSyntax, Initializer: not null })
        {
            return false;
        }

        // Check if all initializer expressions are simple
        for (int i = 0; i < arrayCreationExpr.Initializer.Expressions.Count; i++)
        {
            ExpressionSyntax expression = arrayCreationExpr.Initializer.Expressions[i];
            if (!IsSimpleExpression(expression, semanticModel))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSimpleCollectionInitialization(ObjectCreationExpressionSyntax objectCreationExpr, SemanticModel semanticModel)
    {
        // Check if the type is a collection type (e.g., List<T>, Dictionary<TKey, TValue>)
        if (objectCreationExpr.Initializer == null)
        {
            return false;
        }

        for (int i = 0; i < objectCreationExpr.Initializer.Expressions.Count; i++)
        {
            ExpressionSyntax? expression = objectCreationExpr.Initializer.Expressions[i];
            if (!IsSimpleCollectionInitializerExpression(expression, semanticModel))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSimpleCollectionInitializerExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        switch (expression)
        {
            case AssignmentExpressionSyntax assignmentExpr:
                // For dictionary initializations
                return IsSimpleExpression(assignmentExpr.Left, semanticModel) && IsSimpleExpression(assignmentExpr.Right, semanticModel);

            case InvocationExpressionSyntax:
                // Method calls are complex
                return false;

            case LiteralExpressionSyntax:
            case IdentifierNameSyntax:
                return true;

            case InitializerExpressionSyntax initializerExpr:
                // For collection of collections
                for (int i = 0; i < initializerExpr.Expressions.Count; i++)
                {
                    ExpressionSyntax expr = initializerExpr.Expressions[i];
                    if (!IsSimpleExpression(expr, semanticModel))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return IsSimpleExpression(expression, semanticModel);
        }
    }

    private static bool IsSimpleExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax:
            case IdentifierNameSyntax:
            case ElementAccessExpressionSyntax:
                return true;

            case InvocationExpressionSyntax invocationExpr:
                // Check if it's a nameof expression
                if (IsNameOfExpression(invocationExpr))
                {
                    return true;
                }

                // Check if the method call is safe
                return IsSafeMethodCall(invocationExpr, semanticModel);

            case ConditionalExpressionSyntax conditionalExpr:
                // Recursively check conditional expressions
                return IsSimpleExpression(conditionalExpr.Condition, semanticModel)
                       && IsSimpleExpression(conditionalExpr.WhenTrue, semanticModel)
                       && IsSimpleExpression(conditionalExpr.WhenFalse, semanticModel);

            case MemberAccessExpressionSyntax memberAccessExpr:
                // Check if the member access is safe
                return IsSafeMemberAccess(memberAccessExpr, semanticModel);

            default:
                // Check if the expression is a compile-time constant
                return semanticModel.IsCompileTimeConstant(expression);
        }
    }

    private static bool IsSimpleImplicitArrayCreation(ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpr, SemanticModel semanticModel)
    {
        for (int i = 0; i < implicitArrayCreationExpr.Initializer.Expressions.Count; i++)
        {
            ExpressionSyntax? expression = implicitArrayCreationExpr.Initializer.Expressions[i];
            if (!IsSimpleExpression(expression, semanticModel))
            {
                return false;
            }
        }

        return true;
    }
}

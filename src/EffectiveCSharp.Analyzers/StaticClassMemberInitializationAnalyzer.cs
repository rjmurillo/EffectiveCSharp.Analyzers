using System.Text;

namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #13 - Use Proper Initialization for Static Class Members.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StaticClassMemberInitializationAnalyzer : DiagnosticAnalyzer
{
#pragma warning disable ECS0200
    private const string DiagnosticId = DiagnosticIds.StaticClassMemberInitialization;
#pragma warning restore ECS0200
    private static readonly LocalizableString Description = "Static fields requiring complex or potentially exception-throwing initialization should be initialized within a static constructor or using Lazy<T>.";
    private static readonly LocalizableString MessageFormat = "Static field '{0}' should be initialized in a static constructor or using Lazy<T>";
    private static readonly LocalizableString Title = "Use proper initialization for static class members";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Categories.Initialization,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticId}.md");

    private static readonly HashSet<string> SafeItems = new(StringComparer.Ordinal)
    {
        // Math
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
        "System.Math.PI",
        "System.Math.E",

        // String
        "System.String.IsNullOrEmpty",
        "System.String.IsNullOrWhiteSpace",

        // Enum
        "System.Enum.GetValues",

        // StringComparer static properties
        "System.StringComparer.Ordinal",
        "System.StringComparer.OrdinalIgnoreCase",
        "System.StringComparer.InvariantCulture",
        "System.StringComparer.InvariantCultureIgnoreCase",
        "System.StringComparer.CurrentCulture",
        "System.StringComparer.CurrentCultureIgnoreCase",

        // CultureInfo static properties
        "System.Globalization.CultureInfo.InvariantCulture",
        "System.Globalization.CultureInfo.CurrentCulture",
        "System.Globalization.CultureInfo.CurrentUICulture",

        // Encoding static properties
        "System.Text.Encoding.Unicode",
        "System.Text.Encoding.BigEndianUnicode",
        "System.Text.Encoding.UTF7",
        "System.Text.Encoding.UTF8",
        "System.Text.Encoding.UTF16",
        "System.Text.Encoding.UTF32",
        "System.Text.Encoding.ASCII",

        "System.Text.RegularExpressions.Regex",

        "System.Version",

        // Guid static fields
        "System.Guid.Empty",

        // Date and Time
        "System.DateTime.Now",

        // TimeSpan static fields
        "System.TimeSpan.Zero",
        "System.TimeSpan.MaxValue",
        "System.TimeSpan.MinValue",

        // DateTime static fields and properties
        "System.DateTime.MinValue",
        "System.DateTime.MaxValue",
        "System.DateTime.UtcNow", // Use caution if you consider this safe

        // Environment static properties (use with caution)
        "System.Environment.NewLine",
        "System.Environment.MachineName",
        "System.Environment.Is64BitOperatingSystem",

        // Path static fields (use with caution)
        "System.IO.Path.DirectorySeparatorChar",
        "System.IO.Path.AltDirectorySeparatorChar",
        "System.IO.Path.PathSeparator",
        "System.IO.Path.VolumeSeparatorChar",

        // Threading static fields
        "System.Threading.Timeout.InfiniteTimeSpan",
        "System.Threading.Timeout.Infinite",

        // String constants
        "System.String.Empty",

        // Version static property
        "System.Environment.Version",
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
                // Safe Items may mutate due to different global configurations for different projects
                // Create a local copy of SafeItems for this compilation with the configuration values applied
                HashSet<string> safeItems = SafeItems;

                IEnumerable<string> additionalSafeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(compilationContext.Options);
                safeItems.UnionWith(additionalSafeMethods);

                // Register action to analyze field declarations
                compilationContext.RegisterSyntaxNodeAction(
                    ctx => AnalyzeFieldDeclaration(ctx, safeItems.ToImmutableHashSet(StringComparer.Ordinal)),
                    SyntaxKind.FieldDeclaration);
            });
    }

    private static void AnalyzeFieldDeclaration(
        SyntaxNodeAnalysisContext context,
        ImmutableHashSet<string> safeItems)
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
            if (!IsComplexInitializer(initializer, safeItems, semanticModel, context.CancellationToken))
            {
                continue;
            }

            // Report diagnostic
            Diagnostic diagnostic = variable.GetLocation().CreateDiagnostic(Rule, variable.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsComplexInitializer(ExpressionSyntax initializer, ImmutableHashSet<string> safeItems, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        (SymbolInfo symbolInfo, bool safe) = IsSafeSymbol(initializer, safeItems, semanticModel, cancellationToken);

        if (safe)
        {
            return false;
        }

        switch (initializer)
        {
            // Simple initializers are literals or simple binary expressions
            case LiteralExpressionSyntax:
            case PrefixUnaryExpressionSyntax:
            case PostfixUnaryExpressionSyntax:
                return false;

            case BinaryExpressionSyntax binaryExpr:
                // Check if both sides are literals or simple identifiers
                return !(IsSimpleExpression(binaryExpr.Left, safeItems, semanticModel, symbolInfo, cancellationToken)
                         && IsSimpleExpression(binaryExpr.Right, safeItems, semanticModel, symbolInfo, cancellationToken));

            case InvocationExpressionSyntax invocationExpr:
                if (IsNameOfExpression(invocationExpr))
                {
                    return false; // Not complex
                }

                // Check if the method call is safe
                return !IsSafeMethodCall(invocationExpr, semanticModel, symbolInfo, cancellationToken);

            case BaseObjectCreationExpressionSyntax objectCreationExpr:
                // Handle object creations and collection initializations
                return !IsSimpleCollectionInitialization(objectCreationExpr, safeItems, semanticModel, cancellationToken);

            case ArrayCreationExpressionSyntax arrayCreationExpr:
                // Handle simple array creation
                return !IsSimpleArrayCreation(arrayCreationExpr, safeItems, semanticModel, cancellationToken);

            case ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpr:
                // Handle simple implicit array creation
                return !IsSimpleImplicitArrayCreation(implicitArrayCreationExpr, safeItems, semanticModel, symbolInfo, cancellationToken);

            case ConditionalExpressionSyntax conditionalExpr:
                // Check if condition, whenTrue, and whenFalse are simple
                return !(IsSimpleExpression(conditionalExpr.Condition, safeItems, semanticModel, symbolInfo, cancellationToken)
                         && IsSimpleExpression(conditionalExpr.WhenTrue, safeItems, semanticModel, symbolInfo, cancellationToken)
                         && IsSimpleExpression(conditionalExpr.WhenFalse, safeItems, semanticModel, symbolInfo, cancellationToken));

            default:
                // Check if the expression is a compile-time constant
                bool isConstant = semanticModel.IsCompileTimeConstant(initializer, cancellationToken);

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

    private static bool IsSafeMemberAccess(SymbolInfo symbolInfo, ImmutableHashSet<string> safeItems)
    {
        // Get the symbol info for the member access expression
        ISymbol? symbol = symbolInfo.Symbol;

        switch (symbol)
        {
            case IPropertySymbol propertySymbol:
                return IsSafeProperty(propertySymbol, safeItems);

            case IFieldSymbol fieldSymbol:
                return IsSafeField(fieldSymbol);

            case IMethodSymbol { MethodKind: MethodKind.PropertyGet } methodSymbol:
                // Handle property getter methods
                if (methodSymbol.AssociatedSymbol is IPropertySymbol associatedProperty)
                {
                    return IsSafeProperty(associatedProperty, safeItems);
                }

                return false;

            default:
                return false;
        }
    }

    private static bool IsSafeMethodCall(InvocationExpressionSyntax invocationExpr, SemanticModel semanticModel, SymbolInfo symbolInfo, CancellationToken cancellationToken)
    {
        // Get the symbol info for the invocation expression
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return false; // Unable to determine method symbol; consider complex
        }

        INamedTypeSymbol? containingType = methodSymbol.ContainingType;
        if (containingType == null)
        {
            return false;
        }

        // Check if all arguments are compile-time constants
        for (int i = 0; i < invocationExpr.ArgumentList.Arguments.Count; i++)
        {
            ArgumentSyntax argument = invocationExpr.ArgumentList.Arguments[i];
            if (!semanticModel.IsCompileTimeConstant(argument.Expression, cancellationToken))
            {
                return false; // Argument is not a compile-time constant
            }
        }

        return false;
    }

    private static bool IsSafeProperty(IPropertySymbol propertySymbol, ImmutableHashSet<string> safeItems)
    {
        if (safeItems.Contains(propertySymbol.ToDisplayString()))
        {
            return true;
        }

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
            string containingTypeName = propertySymbol.ContainingType.ToDisplayString() + "." + propertySymbol.Name;

            // For example, DateTime.Now.Hour
            if (string.Equals(containingTypeName, "System.DateTime", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSimpleArrayCreation(ArrayCreationExpressionSyntax arrayCreationExpr, ImmutableHashSet<string> safeItems, SemanticModel semanticModel, CancellationToken cancellationToken)
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
            if (!IsSimpleExpression(expression, safeItems, semanticModel, semanticModel.GetSymbolInfo(expression, cancellationToken), cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSimpleCollectionInitialization(
        BaseObjectCreationExpressionSyntax objectCreationExpr,
        ImmutableHashSet<string> safeItems,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        // Check if all constructor arguments are simple expressions
        if (objectCreationExpr.ArgumentList != null)
        {
            for (int i = 0; i < objectCreationExpr.ArgumentList.Arguments.Count; i++)
            {
                ArgumentSyntax argument = objectCreationExpr.ArgumentList.Arguments[i];
                (SymbolInfo symbolInfo, bool safe) = IsSafeSymbol(argument.Expression, safeItems, semanticModel, cancellationToken);

                if (safe)
                {
                    continue;
                }

                if (!IsSimpleExpression(argument.Expression, safeItems, semanticModel, symbolInfo, cancellationToken))
                {
                    return false;
                }
            }
        }

        // Check if the type is a collection type (e.g., List<T>, Dictionary<TKey, TValue>)
        if (objectCreationExpr.Initializer != null)
        {
            for (int i = 0; i < objectCreationExpr.Initializer.Expressions.Count; i++)
            {
                ExpressionSyntax expression = objectCreationExpr.Initializer.Expressions[i];
                (SymbolInfo symbolInfo, bool safe) = IsSafeSymbol(expression, safeItems, semanticModel, cancellationToken);

                if (safe)
                {
                    continue;
                }

                if (!IsSimpleCollectionInitializerExpression(expression, safeItems, semanticModel, symbolInfo, cancellationToken))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsSimpleCollectionInitializerExpression(
        ExpressionSyntax expression,
        ImmutableHashSet<string> safeItems,
        SemanticModel semanticModel,
        SymbolInfo symbolInfo,
        CancellationToken cancellationToken)
    {
        switch (expression)
        {
            case AssignmentExpressionSyntax assignmentExpr:
                // For dictionary initializations
                return IsSimpleExpression(assignmentExpr.Left, safeItems, semanticModel, symbolInfo, cancellationToken)
                       && IsSimpleExpression(assignmentExpr.Right, safeItems, semanticModel, symbolInfo, cancellationToken);

            case InvocationExpressionSyntax:
                // Method calls are complex
                return false;

            case LiteralExpressionSyntax:
            case IdentifierNameSyntax:
            case MemberAccessExpressionSyntax:
                return true;

            case InitializerExpressionSyntax initializerExpr:
                // For collection of collections
                for (int i = 0; i < initializerExpr.Expressions.Count; i++)
                {
                    ExpressionSyntax expr = initializerExpr.Expressions[i];
                    if (!IsSimpleExpression(expr, safeItems, semanticModel, symbolInfo, cancellationToken))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return IsSimpleExpression(expression, safeItems, semanticModel, symbolInfo, cancellationToken);
        }
    }

    private static bool IsSimpleExpression(
        ExpressionSyntax expression,
        ImmutableHashSet<string> safeItems,
        SemanticModel semanticModel,
        SymbolInfo symbolInfo,
        CancellationToken cancellationToken)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax:
            case IdentifierNameSyntax:
            case ElementAccessExpressionSyntax:
                return true;

            case MemberAccessExpressionSyntax:

                // Check if the member access is safe
                return IsSafeMemberAccess(symbolInfo, safeItems);

            case InvocationExpressionSyntax invocationExpr:
                // Check if it's a nameof expression
                if (IsNameOfExpression(invocationExpr))
                {
                    return true;
                }

                // Check if the method call is safe
                (_, bool isSafe) = IsSafeSymbol(invocationExpr, safeItems, semanticModel, cancellationToken);

                if (isSafe)
                {
                    return true;
                }

                // Check if the method call is safe
                return IsSafeMethodCall(invocationExpr, semanticModel, symbolInfo, cancellationToken);

            case ObjectCreationExpressionSyntax objectCreationExpr:
                // Handle object creations with simple constructor arguments
                if (objectCreationExpr.ArgumentList == null)
                {
                    return true;
                }

                for (int i = 0; i < objectCreationExpr.ArgumentList.Arguments.Count; i++)
                {
                    ArgumentSyntax argument = objectCreationExpr.ArgumentList.Arguments[i];
                    if (!IsSimpleExpression(argument.Expression, safeItems, semanticModel, symbolInfo, cancellationToken))
                    {
                        return false;
                    }
                }

                return true;

            case ConditionalExpressionSyntax conditionalExpr:
                // Recursively check conditional expressions
                return IsSimpleExpression(conditionalExpr.Condition, safeItems, semanticModel, symbolInfo, cancellationToken)
                       && IsSimpleExpression(conditionalExpr.WhenTrue, safeItems, semanticModel, symbolInfo, cancellationToken)
                       && IsSimpleExpression(conditionalExpr.WhenFalse, safeItems, semanticModel, symbolInfo, cancellationToken);

            case BinaryExpressionSyntax binaryExpr:
                return IsSimpleExpression(binaryExpr.Left, safeItems, semanticModel, symbolInfo, cancellationToken)
                       && IsSimpleExpression(binaryExpr.Right, safeItems, semanticModel, symbolInfo, cancellationToken);

            default:
                // Check if the expression is a compile-time constant
                return semanticModel.IsCompileTimeConstant(expression, cancellationToken);
        }
    }

    private static (SymbolInfo Symbol, bool Safe) IsSafeSymbol(
    ExpressionSyntax expression,
    ImmutableHashSet<string> safeItems,
    SemanticModel semanticModel,
    CancellationToken cancellationToken)
    {
        // Get the symbol associated with the expression
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(expression, cancellationToken);
        ISymbol? symbol = symbolInfo.Symbol;

        if (symbol == null)
        {
            return (symbolInfo, false);
        }

        // Use a local StringBuilder to build symbol names
        StringBuilder builder = new StringBuilder();

        // Build the fully qualified name of the symbol
        BuildFullName(symbol, builder);
        string symbolFullName = builder.ToString();

        if (safeItems.Contains(symbolFullName))
        {
            return (symbolInfo, true);
        }

        // Clear the builder for reuse
        builder.Clear();

        // If the symbol is a type, check if the type name is in SafeItems
        if (symbol is INamedTypeSymbol typeSymbol)
        {
            BuildFullName(typeSymbol, builder);
            string typeFullName = builder.ToString();

            if (safeItems.Contains(typeFullName))
            {
                return (symbolInfo, true);
            }
        }
        else if (symbol.ContainingType != null)
        {
            // Build the containing type's full name
            builder.Clear();
            BuildFullName(symbol.ContainingType, builder);
            string containingTypeFullName = builder.ToString();

            if (safeItems.Contains(containingTypeFullName))
            {
                return (symbolInfo, true);
            }

            // Append the member name to get the full member name
            builder.Append('.');
            builder.Append(symbol.Name);
            string memberFullName = builder.ToString();

            if (safeItems.Contains(memberFullName))
            {
                return (symbolInfo, true);
            }
        }

        return (symbolInfo, false);
    }

    private static void BuildFullName(ISymbol symbol, StringBuilder builder)
    {
        if (symbol.ContainingNamespace is { IsGlobalNamespace: false })
        {
            BuildNamespaceName(symbol.ContainingNamespace, builder);
            builder.Append('.');
        }
        else if (symbol.ContainingType != null)
        {
            BuildFullName(symbol.ContainingType, builder);
            builder.Append('.');
        }

        builder.Append(symbol.MetadataName);
    }

    private static void BuildNamespaceName(INamespaceSymbol namespaceSymbol, StringBuilder builder)
    {
        if (namespaceSymbol.ContainingNamespace is { IsGlobalNamespace: false })
        {
            BuildNamespaceName(namespaceSymbol.ContainingNamespace, builder);
            builder.Append('.');
        }

        builder.Append(namespaceSymbol.Name);
    }


    private static bool IsSimpleImplicitArrayCreation(
        ImplicitArrayCreationExpressionSyntax implicitArrayCreationExpr,
        ImmutableHashSet<string> safeItems,
        SemanticModel semanticModel,
        SymbolInfo symbolInfo,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < implicitArrayCreationExpr.Initializer.Expressions.Count; i++)
        {
            ExpressionSyntax expression = implicitArrayCreationExpr.Initializer.Expressions[i];
            if (!IsSimpleExpression(expression, safeItems, semanticModel, symbolInfo, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}

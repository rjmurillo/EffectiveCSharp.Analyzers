namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #9 - Minimize boxing and unboxing.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MinimizeBoxingUnboxingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MinimizeBoxingUnboxing,
        title: "Minimize boxing and unboxing",
        messageFormat: "Consider using an alternative implementation to avoid boxing and unboxing",
        category: Categories.Performance,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticIds.MinimizeBoxingUnboxing}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
        {
            INamedTypeSymbol? dictionarySymbol = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
            INamedTypeSymbol? listSymbol = compilationStartAnalysisContext.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");

            compilationStartAnalysisContext.RegisterOperationAction(AnalyzeOperation, OperationKind.Conversion);
            compilationStartAnalysisContext.RegisterSyntaxNodeAction(
                syntaxNodeContext => AnalyzeNode(syntaxNodeContext, dictionarySymbol, listSymbol),
                SyntaxKind.ElementAccessExpression,
                SyntaxKind.AddExpression);
        });
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context, INamedTypeSymbol? dictionarySymbol, INamedTypeSymbol? listSymbol)
    {
        if (context.Node is BinaryExpressionSyntax binaryExpr
            && binaryExpr.IsKind(SyntaxKind.AddExpression)
            && context.SemanticModel.GetTypeInfo(binaryExpr.Left, context.CancellationToken).Type?.SpecialType == SpecialType.System_String)
        {
            // Check both sides for the addition of a string to something else
            TypeInfo leftInfo = context.SemanticModel.GetTypeInfo(binaryExpr.Left, context.CancellationToken);
            TypeInfo rightInfo = context.SemanticModel.GetTypeInfo(binaryExpr.Right, context.CancellationToken);

            // If either side is a string and the other is a value type that is converted to string, it's safe
            if ((leftInfo.Type?.SpecialType == SpecialType.System_String || rightInfo.Type?.SpecialType == SpecialType.System_String) &&
                (leftInfo.Type?.IsValueType == true || rightInfo.Type?.IsValueType == true))
            {
                // Exclude string concatenation with value types that automatically call ToString
                return;
            }
        }

        if (context.Node is not ElementAccessExpressionSyntax elementAccess)
        {
            return;
        }

        // Get the type of the accessed object
        TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(elementAccess.Expression, context.CancellationToken);

        if (typeInfo.Type is not INamedTypeSymbol { IsGenericType: true } namedType)
        {
            return;
        }

        INamedTypeSymbol baseType = namedType.ConstructedFrom;
        if (SymbolEqualityComparer.Default.Equals(baseType, dictionarySymbol))
        {
            ITypeSymbol keyType = namedType.TypeArguments[0]; // The TKey in Dictionary<TKey, TValue>
            if (ReportDiagnosticOnValueType(keyType, ref context, elementAccess))
            {
                return;
            }

            ITypeSymbol valueType = namedType.TypeArguments[1]; // The TValue in Dictionary<TKey, TValue>
            if (ReportDiagnosticOnValueType(valueType, ref context, elementAccess))
            {
                return;
            }
        }
        else if (SymbolEqualityComparer.Default.Equals(baseType, listSymbol))
        {
            ITypeSymbol elementType = namedType.TypeArguments[0]; // The T in List<T>
            if (ReportDiagnosticOnValueType(elementType, ref context, elementAccess))
            {
                return;
            }
        }
    }

    private static bool ReportDiagnosticOnValueType(ITypeSymbol? typeSymbol, ref SyntaxNodeAnalysisContext context, ElementAccessExpressionSyntax elementAccess)
    {
        // Check if the struct is read/write; if so, there can be bad things that happen to warn
        if (typeSymbol is not { IsValueType: true, IsReadOnly: false })
        {
            return false;
        }

        // Create and report a diagnostic if the element is accessed directly
        Diagnostic diagnostic = elementAccess.GetLocation().CreateDiagnostic(Rule, typeSymbol.Name);
        context.ReportDiagnostic(diagnostic);

        return true;
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        if (context.Operation is IConversionOperation conversionOperation)
        {
            AnalyzeConversionOperation(conversionOperation, context);
        }
        else
        {
            throw new NotSupportedException($"Unsupported operation kind: {context.Operation.Kind}");
        }
    }

    private static void AnalyzeConversionOperation(IConversionOperation conversionOperation, OperationAnalysisContext context)
    {
        // We need to detect when a conversion operation should indeed considered to be problematic:
        //
        // 1. Excluding safe operations: Some conversions might be misidentified as boxing when they are safe or optimized
        // away by the compiler, such as converting between numeric types or appending integers to strings which only involves
        // calling `.ToString()`.
        // 2. Context-Sensitive: Depending on the context of the conversion (like within a string concatenation), it might be
        // considered boxing. Analyzing the parent operations or the usage context is needed to decide to trigger a boxing warning
        // 3. Specific type checks: before reporting boxing, verify the types involved in the conversion are not special cases
        // that are handled differently, like enum to string conversions in switch statements or using `int` in `string` concatentation

        // Check if the conversion explicitly involves boxing or unboxing
        if (!conversionOperation.IsBoxingOrUnboxingOperation())
        {
            return;  // Skip conversions that do not involve boxing or unboxing
        }

        // Further refinement: Check the usage context to avoid false positives
        // Example: Avoid flagging string concatenations that do not actually box
        if (conversionOperation.Parent is IBinaryOperation binaryOperation
            && binaryOperation.OperatorKind == BinaryOperatorKind.Add
            && binaryOperation.Type?.SpecialType == SpecialType.System_String)
        {
            ITypeSymbol? operandType = conversionOperation.Operand.Type;
            if (operandType?.IsValueType == true && operandType.SpecialType != SpecialType.System_String)
            {
                // Typically, int + string triggers ToString() without boxing
                return;
            }
        }

        Diagnostic diagnostic = conversionOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
        context.ReportDiagnostic(diagnostic);
    }
}

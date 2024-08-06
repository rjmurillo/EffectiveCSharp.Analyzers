namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #9 - Minimize boxing and unboxing.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MinimizeBoxingUnboxingAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.MinimizeBoxingUnboxing;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Minimize boxing and unboxing",
        messageFormat: "Consider using an alternative implementation to avoid boxing and unboxing",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

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
                SyntaxKind.ElementAccessExpression);
        });
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context, INamedTypeSymbol? dictionarySymbol, INamedTypeSymbol? listSymbol)
    {
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
            if (ReportDiagnosticOnValueType(keyType))
            {
                return;
            }

            ITypeSymbol valueType = namedType.TypeArguments[1]; // The TValue in Dictionary<TKey, TValue>
            if (ReportDiagnosticOnValueType(valueType))
            {
                return;
            }
        }
        else if (SymbolEqualityComparer.Default.Equals(baseType, listSymbol))
        {
            ITypeSymbol elementType = namedType.TypeArguments[0]; // The T in List<T>
            if (ReportDiagnosticOnValueType(elementType))
            {
                return;
            }
        }

        return;

        bool ReportDiagnosticOnValueType(ITypeSymbol? typeSymbol)
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
        if (conversionOperation.IsBoxingOrUnboxingOperation())
        {
            Diagnostic diagnostic = conversionOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }
}

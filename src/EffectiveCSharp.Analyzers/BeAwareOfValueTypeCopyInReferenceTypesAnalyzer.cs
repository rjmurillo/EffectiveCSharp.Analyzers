using System.Collections;

namespace EffectiveCSharp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BeAwareOfValueTypeCopyInReferenceTypesAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.BeAwareOfValueTypeCopyInReferenceTypes;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Be aware of value type copy in reference types",
        messageFormat: "Consider using an alternative implementation to avoid copying value type '{0}' to the heap",
        description: "Detects when value types may be stored on the heap.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers{ThisAssembly.GitCommitId}/docs/{Id}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ElementAccessExpression);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        ElementAccessExpressionSyntax elementAccess = (ElementAccessExpressionSyntax)context.Node;

        // Get the type of the accessed object
        TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(elementAccess.Expression, context.CancellationToken);

        if (typeInfo.Type is not INamedTypeSymbol { IsGenericType: true } namedType)
        {
            return;
        }

        string constructedTypeName = namedType.ConstructedFrom.ToString();
        ITypeSymbol? elementType = null;

        // Check if it's a List<T>
        if (string.Equals(constructedTypeName, "System.Collections.Generic.List<T>", StringComparison.Ordinal))
        {
            elementType = namedType.TypeArguments[0];
        }

        // Check if the element type is a value type
        if (elementType is { IsValueType: true })
        {
            // Create and report a diagnostic if the element is accessed directly
            Diagnostic diagnostic = elementAccess.GetLocation().CreateDiagnostic(Rule, elementType.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private bool IsCollection(ITypeSymbol typeSymbol)
    {
        // You can expand this method to check for other collection types
        return typeSymbol.AllInterfaces.Any(i =>
            (i.MetadataName == nameof(IEnumerable) && i.ContainingNamespace.Name == "System.Collections") ||
            (i.MetadataName == nameof(IEnumerable) && i.ContainingNamespace.Name == "System.Collections.Generic"));
    }
}

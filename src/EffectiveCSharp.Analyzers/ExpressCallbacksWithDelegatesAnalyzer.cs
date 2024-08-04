using System;
using System.Collections.Generic;
using System.Text;

namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #7 - Express callbacks with delegates.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ExpressCallbacksWithDelegatesAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.ExpressCallbacksWithDelegates;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Express callbacks with delegates",
        messageFormat: "Method '{0}' should use a delegate for the callback",
        description: "Ensure that callbacks are implemented using delegates.",
        category: "Design",
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
        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocationExpr, context.CancellationToken);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

        if (methodSymbol == null)
            return;

        // Check if the method has delegate parameters
        bool hasDelegateParameter = methodSymbol.Parameters.Any(p => IsDelegateType(p.Type));
        if (!hasDelegateParameter)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(Rule, invocationExpr.GetLocation(), methodSymbol.Name);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsDelegateType(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol == null)
        {
            return false;
        }

        if (typeSymbol.TypeKind == TypeKind.Delegate)
        {
            return true;
        }

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            // Handle Func<T>, Action<T>, Predicate<T>
            if (namedTypeSymbol.ConstructedFrom != null &&
                (namedTypeSymbol.ConstructedFrom.Name.StartsWith("Func") ||
                 namedTypeSymbol.ConstructedFrom.Name.StartsWith("Action") ||
                 namedTypeSymbol.ConstructedFrom.Name.StartsWith("Predicate")))
            {
                return true;
            }

            return namedTypeSymbol.ConstructedFrom.SpecialType == SpecialType.System_MulticastDelegate;
        }

        return false;
    }
}

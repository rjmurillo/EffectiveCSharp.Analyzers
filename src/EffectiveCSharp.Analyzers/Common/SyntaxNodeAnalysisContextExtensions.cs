namespace EffectiveCSharp.Analyzers.Common;

internal static class SyntaxNodeAnalysisContextExtensions
{
    internal static bool IsDefaultInitialization(
        this SyntaxNodeAnalysisContext context,
        ITypeSymbol fieldType,
        ExpressionSyntax expressionSyntaxNode)
    {
        return context.SemanticModel.IsDefaultInitialization(fieldType, expressionSyntaxNode, context.CancellationToken);
    }
}

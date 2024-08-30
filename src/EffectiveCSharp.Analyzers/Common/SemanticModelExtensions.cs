namespace EffectiveCSharp.Analyzers.Common;

internal static class SemanticModelExtensions
{
    internal static bool AreExpressionsEquivalent(this SemanticModel semanticModel, ExpressionSyntax left, ExpressionSyntax right)
    {
        // This method checks if the two expressions represent the same value/initialization
        IOperation? leftOperation = semanticModel.GetOperation(left);
        IOperation? rightOperation = semanticModel.GetOperation(right);

        // Compare the operations for semantic equivalence
        return leftOperation != null && rightOperation != null && leftOperation.Kind == rightOperation.Kind && leftOperation.ConstantValue.Equals(rightOperation.ConstantValue);
    }
}

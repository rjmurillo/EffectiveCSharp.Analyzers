namespace EffectiveCSharp.Analyzers.Common;

internal static class IOperationExtensions
{
    internal static bool IsBoxingOperation(this IOperation? operation)
        => IsBoxingOperation(operation as IConversionOperation);

    internal static bool IsConstant(this IOperation operation)
        => operation.ConstantValue.HasValue;

    private static bool IsBoxingOperation(this IConversionOperation? conversionOperation)
        => conversionOperation is { Operand.Type.IsValueType: true, Type.IsReferenceType: true };
}

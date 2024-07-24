namespace EffectiveCSharp.Analyzers.Common;

internal static class IOperationExtensions
{
    /// <summary>
    /// Determines if a given operation involves boxing through type conversion.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns>True if the operation is a boxing conversion, otherwise false.</returns>
    internal static bool IsBoxingOperation(this IOperation? operation)
        => operation is IConversionOperation { Operand.Type.IsValueType: true, Type.IsReferenceType: true };

    /// <summary>
    /// Determines if a given operation involves unboxing through type conversion.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns>True if the operation is an unboxing conversion, otherwise false.</returns>
    internal static bool IsUnboxingOperation(this IOperation? operation)
        => operation is IConversionOperation { Operand.Type.IsReferenceType: true, Type.IsValueType: true };

    /// <summary>
    /// Determines if a given operation involves boxing or unboxing through type conversion.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns>True if the operation is a boxing or unboxing conversion, otherwise false.</returns>
    internal static bool IsBoxingOrUnboxingOperation(this IOperation? operation)
        => operation.IsBoxingOperation() || operation.IsUnboxingOperation();
}

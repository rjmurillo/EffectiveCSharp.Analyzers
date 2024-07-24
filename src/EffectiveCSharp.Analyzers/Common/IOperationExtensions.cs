namespace EffectiveCSharp.Analyzers.Common;

internal static class IOperationExtensions
{
    /// <summary>
    /// Determines if a given operation involves boxing through type conversion.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns>True if the operation is a boxing conversion, otherwise false.</returns>
    /// <seealso cref="IsBoxingOperation(IConversionOperation?)"/>
    internal static bool IsBoxingOperation(this IOperation? operation)
        => IsBoxingOperation(operation as IConversionOperation);

    /// <summary>
    /// Checks if an operation involves a constant value.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns>True if the operation has a constant value, otherwise false.</returns>
    internal static bool IsConstant(this IOperation operation)
        => operation.ConstantValue.HasValue;

    /// <summary>
    /// Identifies if an assignment operation targets a type that could require boxing.
    /// </summary>
    /// <param name="assignmentOperation">The assignment operation to check.</param>
    /// <returns>True if the target type is an object or interface, otherwise false.</returns>
    internal static bool IsAssignmentToBoxedType(this ISimpleAssignmentOperation assignmentOperation)
    {
        return assignmentOperation.Target.Type?.SpecialType == SpecialType.System_Object
               || assignmentOperation.Target.Type?.TypeKind == TypeKind.Interface;
    }

    /// <summary>
    /// Checks if an argument in a method invocation is passed to a parameter that could require boxing.
    /// </summary>
    /// <param name="argumentOperation">The argument operation to check.</param>
    /// <returns>True if the parameter type is an object or interface, otherwise false.</returns>
    internal static bool IsArgumentToBoxedType(this IArgumentOperation argumentOperation)
    {
        return argumentOperation.Parameter?.Type.SpecialType == SpecialType.System_Object
               || argumentOperation.Parameter?.Type.TypeKind == TypeKind.Interface;
    }

    /// <summary>
    /// Determines if a return operation involves returning a value that could require boxing.
    /// </summary>
    /// <param name="returnOperation">The return operation to check.</param>
    /// <returns>True if the returned value's type is an object or interface, otherwise false.</returns>
    internal static bool IsReturnToBoxedType(this IReturnOperation returnOperation)
    {
        return returnOperation.ReturnedValue?.Type?.SpecialType == SpecialType.System_Object
               || returnOperation.ReturnedValue?.Type?.TypeKind == TypeKind.Interface;
    }

    /// <summary>
    /// Identifies if an invocation operation involves a method that returns a type requiring boxing or if the instance on which the method is called is a value type.
    /// </summary>
    /// <param name="invocationOperation">The invocation operation to check.</param>
    /// <returns>True if the return type is an object or interface and the instance type is a value type, otherwise false.</returns>
    internal static bool IsInvocationToBoxedType(this IInvocationOperation invocationOperation)
    {
        return (invocationOperation.TargetMethod.ReturnType?.SpecialType == SpecialType.System_Object
               || invocationOperation.TargetMethod.ReturnType?.TypeKind == TypeKind.Interface)
               && invocationOperation.Instance?.Type?.IsValueType == true;
    }

    /// <summary>
    /// Private helper to determine if a conversion operation is a boxing conversion.
    /// </summary>
    /// <param name="conversionOperation">The conversion operation to check.</param>
    /// <returns>True if the operation converts from a value type to a reference type, otherwise false.</returns>
    private static bool IsBoxingOperation(this IConversionOperation? conversionOperation)
        => conversionOperation is { Operand.Type.IsValueType: true, Type.IsReferenceType: true };
}

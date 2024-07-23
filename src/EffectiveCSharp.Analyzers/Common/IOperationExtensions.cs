namespace EffectiveCSharp.Analyzers.Common;

internal static class IOperationExtensions
{
    internal static bool IsBoxingOperation(this IOperation? operation)
        => IsBoxingOperation(operation as IConversionOperation);

    internal static bool IsConstant(this IOperation operation)
        => operation.ConstantValue.HasValue;

    internal static bool IsAssignmentToBoxedType(this ISimpleAssignmentOperation assignmentOperation)
    {
        return assignmentOperation.Target.Type?.SpecialType == SpecialType.System_Object
               || assignmentOperation.Target.Type?.TypeKind == TypeKind.Interface;
    }

    internal static bool IsArgumentToBoxedType(this IArgumentOperation argumentOperation)
    {
        return argumentOperation.Parameter?.Type.SpecialType == SpecialType.System_Object
               || argumentOperation.Parameter?.Type.TypeKind == TypeKind.Interface;
    }

    internal static bool IsReturnToBoxedType(this IReturnOperation returnOperation)
    {
        return returnOperation.ReturnedValue?.Type?.SpecialType == SpecialType.System_Object
               || returnOperation.ReturnedValue?.Type?.TypeKind == TypeKind.Interface;
    }

    internal static bool IsInvocationToBoxedType(this IInvocationOperation invocationOperation)
    {
        return (invocationOperation.TargetMethod.ReturnType?.SpecialType == SpecialType.System_Object
               || invocationOperation.TargetMethod.ReturnType?.TypeKind == TypeKind.Interface)
               && invocationOperation.Instance?.Type?.IsValueType == true;
    }

    private static bool IsBoxingOperation(this IConversionOperation? conversionOperation)
        => conversionOperation is { Operand.Type.IsValueType: true, Type.IsReferenceType: true };
}

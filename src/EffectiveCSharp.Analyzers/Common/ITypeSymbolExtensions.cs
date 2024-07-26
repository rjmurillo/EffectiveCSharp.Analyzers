namespace EffectiveCSharp.Analyzers.Common;

internal static class ITypeSymbolExtensions
{
    public static bool IsNumericType(this ITypeSymbol? type)
    {
        return type?.SpecialType is SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal;
    }
}

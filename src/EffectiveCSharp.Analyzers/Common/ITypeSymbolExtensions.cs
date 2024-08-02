namespace EffectiveCSharp.Analyzers.Common;

internal static class ITypeSymbolExtensions
{
    internal static bool IsNumericType(this ITypeSymbol? type)
    {
        return type?.SpecialType is SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal;
    }

    /// <summary>
    /// Returns true if the type is <see cref="string"/>.
    /// </summary>
    internal static bool IsString(this ITypeSymbol typeSymbol)
    {
        return typeSymbol?.SpecialType == SpecialType.System_String;
    }
}

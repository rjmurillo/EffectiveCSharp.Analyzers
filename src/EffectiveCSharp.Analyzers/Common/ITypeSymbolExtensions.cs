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
    /// Determines if the <paramref name="typeSymbol"/> is <see cref="string"/>.
    /// </summary>
    /// <param name="typeSymbol">The type.</param>
    /// <returns>Return true if the type is <see cref="string"/>; otherwise, false.</returns>
    internal static bool IsString(this ITypeSymbol typeSymbol)
    {
        return typeSymbol?.SpecialType == SpecialType.System_String;
    }
}

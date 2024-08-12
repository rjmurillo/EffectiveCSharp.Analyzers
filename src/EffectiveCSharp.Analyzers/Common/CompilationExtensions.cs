namespace EffectiveCSharp.Analyzers.Common;

internal static class CompilationExtensions
{
    internal static bool IsCSharpVersionOrLater(this Compilation compilation, LanguageVersion version)
    {
        CSharpParseOptions? parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        return parseOptions != null && parseOptions.LanguageVersion >= version;
    }
}

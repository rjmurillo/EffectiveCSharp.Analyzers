using System.Runtime.CompilerServices;

namespace EffectiveCSharp.Analyzers.Common;

internal static class CompilationExtensions
{
    /// <summary>
    /// Gets the language version the compiler is capable of parsing.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>A <see cref="LanguageVersion"/> if there is a <see cref="CSharpParseOptions"/>; otherwise, null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LanguageVersion? GetLanguageVersionFromCompilation(this Compilation compilation)
    {
        return compilation.SyntaxTrees.FirstOrDefault()?.Options is not CSharpParseOptions parseOptions
            ? null
            : parseOptions.LanguageVersion;
    }

    /// <summary>
    /// Gets the specific <paramref name="typeName"/>.
    /// </summary>
    /// <param name="compilation">The <see cref="Compilation"/> instance.</param>
    /// <param name="typeName">Fully qualified name of the type.</param>
    /// <returns>True if the type can be located; otherwise, false.</returns>
    /// <seealso cref="Compilation.GetTypeByMetadataName"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool SupportsType(this Compilation compilation, string typeName)
    {
        INamedTypeSymbol? symbol = compilation.GetTypeByMetadataName(typeName);
        return symbol != null;
    }

    /// <summary>
    /// Gets the specific <paramref name="typeName"/>.
    /// </summary>
    /// <param name="compilation">The <see cref="Compilation"/> instance.</param>
    /// <param name="typeName">Fully qualified name of the type.</param>
    /// <param name="memberName">Name of the member on the type.</param>
    /// <returns>True if the type with the specified member can be located; otherwise, false.</returns>
    /// <seealso cref="Compilation.GetTypeByMetadataName"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool SupportsType(this Compilation compilation, string typeName, string memberName)
    {
        INamedTypeSymbol? symbol = compilation.GetTypeByMetadataName(typeName);
        return symbol?.GetMembers(memberName).Length > 0;
    }

    /// <summary>
    /// Gets the version from the compilation's referenced types.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>A <see cref="Version"/> if there is a <see cref="System.Object"/> that can be located; otherwise, performs feature detection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Version? GetDotNetVersionFromCompilation(this Compilation compilation)
    {
        Version mscorlibVersion = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly.Identity.Version;

        // If we detect a .NET version newer than 4, then just return that.
        if (mscorlibVersion > DotNet.Versions.DotNet40)
        {
            return mscorlibVersion;
        }

        // The assembly version of mscorlib would be `4.0.0.0` regardless of the .NET Framework version
        // To differentiate, we need to sniff for specific types or methods that exist only in those .NET versions

        // Check for .NET Framework 4.8+ (introduced System.Runtime.GCLargeObjectHeapCompactionMode)
        bool gcLargeObjectHeapCompactionModeType = compilation.SupportsType("System.Runtime.GCLargeObjectHeapCompactionMode");

        if (gcLargeObjectHeapCompactionModeType)
        {
            // .NET Framework 4.8+
            return DotNet.Versions.DotNet48;
        }

        // Check for .NET Framework 4.7+ (introduced System.ValueTuple)
        bool valueTupleType = compilation.SupportsType("System.ValueTuple");

        if (valueTupleType)
        {
            // .NET Framework 4.7+
            return DotNet.Versions.DotNet47;
        }

        // Check for .NET Framework 4.6.2+ (introduced System.AppContext.SetSwitch)
        bool hasSetSwitch = compilation.SupportsType("System.AppContext", "SetSwitch");

        if (hasSetSwitch)
        {
            // .NET Framework 4.6.2+
            return DotNet.Versions.DotNet46;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsCSharpVersionOrLater(this Compilation compilation, LanguageVersion desiredLanguageVersion)
    {
        LanguageVersion? languageVersion = GetLanguageVersionFromCompilation(compilation);

        return languageVersion.HasValue && languageVersion.Value >= desiredLanguageVersion;
    }

    /// <summary>
    /// Gets the .NET runtime version and the supported compiler language version.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>
    /// A <see cref="Tuple{Version, LanguageVersion}"/>:
    /// <list type="unordered">
    ///     <item>
    ///         <term><see cref="Version"/></term>
    ///         <description>Contains the version of the assembly containing the type <see cref="System.Object"/>.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="LanguageVersion" /></term>
    ///         <description>The effective language version, which the compiler uses to produce the <see cref="SyntaxTree"/>.</description>
    ///     </item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// The .NET version and the language version can differ, for example we can downgrade the language version
    /// on a newer runtime, or downgrade the runtime with a newer language. When picking up issues, we want to
    /// make sure analyzers and code fix providers are finding legitimate issues and offering compatible solutions.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Version? DotNetVersion, LanguageVersion? CompilerLanguageVersion) GetVersions(this Compilation compilation)
    {
        Version? version = GetDotNetVersionFromCompilation(compilation);
        LanguageVersion? lang = GetLanguageVersionFromCompilation(compilation);

        return (version, lang);
    }
}

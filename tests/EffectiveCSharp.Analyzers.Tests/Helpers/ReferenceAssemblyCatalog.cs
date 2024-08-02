namespace EffectiveCSharp.Analyzers.Tests.Helpers;

/// <summary>
/// The testing framework does heavy work to resolve references for set of <see cref="ReferenceAssemblies"/>, including potentially
/// running the NuGet client to download packages. This class caches the ReferenceAssemblies class (which is thread-safe), so that
/// package resolution only happens once for a given configuration.
/// </summary>
/// <remarks>
/// It would be more straightforward to pass around ReferenceAssemblies instances directly, but using non-primitive types causes
/// Visual Studio's Test Explorer to collapse all test cases down to a single entry, which makes it harder to see which test cases
/// are failing or debug a single test case.
/// </remarks>
internal static class ReferenceAssemblyCatalog
{
    // We want to test the supported versions of .NET
    // References
    // https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-framework
    // https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-framework
    // https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
    //
    // As of July 2024, the supported versions are:
    // .NET Framework:
    // 4.8.1 and 4.8
    // 4.7.2, 4.7.1, and 4.7
    // 4.6.2 (ends Jan 12, 2027 because of Windows)
    // 3.5 SP1 (ends Jan 9, 2029 because of Windows)
    // NOTE: 4.5.2, 4.6, 4.6.1 retired on April 26, 2022
    // .NET (formally .NET Core)
    // 8 LTS (ends Nov 10, 2026)
    // 6 LTS (ends Nov 12, 2024)
    // NOTE: 7, 5, Core 3.1, 3.0, 2.2, 2.1, 2.0, 1.1, and 1.0 are no longer supported
    public static string Net48 => nameof(ReferenceAssemblies.NetFramework.Net48);

    public static string Net472 => nameof(ReferenceAssemblies.NetFramework.Net472);

    public static string Net471 => nameof(ReferenceAssemblies.NetFramework.Net471);

    public static string Net47 => nameof(ReferenceAssemblies.NetFramework.Net47);

    public static string Net462 => nameof(ReferenceAssemblies.NetFramework.Net462);

    public static string Net35 => nameof(ReferenceAssemblies.NetFramework.Net35);

    public static string Net60 => nameof(ReferenceAssemblies.Net.Net60);

    public static string Net80 => nameof(ReferenceAssemblies.Net.Net80);

    public static string Net90 => nameof(ReferenceAssemblies.Net.Net90);

    public static string Latest => nameof(ReferenceAssemblies.Net.Net80);

    public static IReadOnlyDictionary<string, ReferenceAssemblies> Catalog { get; } = new Dictionary<string, ReferenceAssemblies>(StringComparer.Ordinal)
    {
        { Net48, ReferenceAssemblies.NetFramework.Net48.Default },
        { Net472, ReferenceAssemblies.NetFramework.Net472.Default },
        { Net471, ReferenceAssemblies.NetFramework.Net471.Default },
        { Net47, ReferenceAssemblies.NetFramework.Net47.Default },
        { Net462, ReferenceAssemblies.NetFramework.Net462.Default },
        { Net35, ReferenceAssemblies.NetFramework.Net35.Default },
        { Net60, ReferenceAssemblies.Net.Net60 },
        { Net80, ReferenceAssemblies.Net.Net80 },
        { Net90, ReferenceAssemblies.Net.Net90 },
    };

    public static IReadOnlySet<string> DotNetCore { get; } = new HashSet<string>(new[] { Net60, Net80, Net90 });
}

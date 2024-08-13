namespace EffectiveCSharp.Analyzers.Common;

internal static class DotNet
{
    /// <summary>For use with feature detection.</summary>
    /// <remarks>When using this with <see cref="CompilationExtensions.GetVersions"/>.</remarks>
    internal static class Versions
    {
        internal static readonly Version DotNet6 = new(6, 0, 0, 0);
        internal static readonly Version DotNet5 = new(5, 0, 0, 0);
        internal static readonly Version DotNet46 = new(4, 6);
        internal static readonly Version DotNet47 = new(4, 7);
        internal static readonly Version DotNet48 = new(4, 8);
        internal static readonly Version DotNet40 = new(4, 0, 0, 0);
    }

    /*
    References
        https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-framework
        https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-framework
        https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
        https://stackoverflow.com/questions/247621/what-are-the-correct-version-numbers-for-c/38506668#38506668

    | C# Version | VS Version        | .NET Version  | CLR Version | Release Date | End of Support |
    |------------|-------------------|---------------|-------------|--------------|----------------|
    | 1.0        | 2002              | 1.0           | 1.0         | Feb 2002     | N/A            |
    | 1.2        | 2003              | 1.1           | 1.1         | Apr 2003     | N/A            |
    | 2.0        | 2005              | 2.0           | 2.0         | Nov 2005     | N/A            |
    | 3.0        | 2008              | 3.5           | 2.0         | Nov 2007     | N/A            |
    | 4.0        | 2010              | 4.0           | 4           | Apr 2010     | N/A            |
    | 5.0        | 2012              | 4.5           | 4           | Aug 2012     | N/A            |
    | 6.0        | 2015              | 4.6           | 4           | Jul 2015     | N/A            |
    | 7.0        | 2017              | 4.6.2         | 4           | Mar 2017     | N/A            |
    | 7.0        | 2017              | .NET Core 1.0 | N/A         | Mar 2017     | Jun 2019       |
    | 7.1        | 2017 (v15.3)      | 4.6.2         | 4           | Aug 2017     | N/A            |
    | 7.1        | 2017 (v15.3)      | .NET Core 2.0 | N/A         | Aug 2017     | Oct 2018       |
    | 7.2        | 2017 (v15.5)      | 4.7.2         | 4           | Dec 2017     | N/A            |
    | 7.3        | 2017 (v15.7)      | 4.7.2         | 4           | May 2018     | N/A            |
    | 7.3        | 2017 (v15.7)      | .NET Core 2.1 | N/A         | May 2018     | Aug 2021       |
    | 8.0        | 2019              | 4.8           | 4           | Apr 2019     | N/A            |
    | 8.0        | 2019              | .NET Core 3.0 | N/A         | Sep 2019     | Mar 2020       |
    | 8.0        | 2019              | .NET Core 3.1 | N/A         | Dec 2019     | Dec 2022       |
    | 9.0        | 2020              | .NET 5        | N/A         | Nov 2020     | May 2022       |
    | 10.0       | 2021              | .NET 6        | N/A         | Nov 2021     | Nov 2024       |
    | 11.0       | 2022 (17.4)       | .NET 7        | N/A         | Nov 2022     | May 2024       |
    | 12.0       | 2023 (17.8)       | .NET 8        | N/A         | Nov 2023     | Nov 2026       |
    */
    internal static class LangVersion
    {
        internal static LanguageVersion? FromDotNetVersion(Version? version)
        {
            return version?.Major switch
            {
                // .NET Framework versions
                4 when version.Minor == 8 => LanguageVersion.CSharp8,
                4 when version is { Minor: 7, Revision: 2 } => LanguageVersion.CSharp7_2,
                4 when version is { Minor: 7, Revision: 1 } => LanguageVersion.CSharp7_1,
                4 when version is { Minor: 7 } => LanguageVersion.CSharp7,
                4 when version is { Minor: 6, Revision: 2 } => LanguageVersion.CSharp7,
                4 when version is { Minor: 6 } => LanguageVersion.CSharp6,

                // .NET Core versions
                5 => LanguageVersion.CSharp9,
                6 => LanguageVersion.CSharp10,
                7 =>

                    // REVIEW: This should be CSharp11, but it's not available in the enum
                    LanguageVersion.CSharp10,
                8 =>

                    // REVIEW: This should be CSharp12, but it's not available in the enum
                    LanguageVersion.CSharp10,

                // .NET Core specific versions
                3 when version.Minor == 1 => LanguageVersion.CSharp8,
                3 when version.Minor == 0 => LanguageVersion.CSharp8,
                2 when version.Minor == 2 => LanguageVersion.CSharp7_3,
                2 when version.Minor == 1 => LanguageVersion.CSharp7_3,
                2 when version.Minor == 0 => LanguageVersion.CSharp7_1,
                1 when version.Minor == 1 => LanguageVersion.CSharp7,
                1 when version.Minor == 0 => LanguageVersion.CSharp7,
                _ => null,
            };
        }
    }
}

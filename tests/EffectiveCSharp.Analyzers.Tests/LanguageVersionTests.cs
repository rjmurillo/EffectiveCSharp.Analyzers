using EffectiveCSharp.Analyzers.Common;

namespace EffectiveCSharp.Analyzers.Tests;

public class LanguageVersionTests
{
    [Theory]
    [InlineData("3.5", LanguageVersion.CSharp3)]
    [InlineData("4.6", LanguageVersion.CSharp6)]
    [InlineData("4.6.2", LanguageVersion.CSharp7)]
    [InlineData("4.7.2", LanguageVersion.CSharp7_3)] // Maps to the last 7.x version
    [InlineData("4.8", LanguageVersion.CSharp8)]
    [InlineData("Core 1.0", LanguageVersion.CSharp7)] // Maps to C# 7
    [InlineData("Core 2.0", LanguageVersion.CSharp7_1)]
    [InlineData("Core 2.1", LanguageVersion.CSharp7_3)]
    [InlineData("Core 3.0", LanguageVersion.CSharp8)]
    [InlineData("Core 3.1", LanguageVersion.CSharp8)]
    [InlineData("5.0", LanguageVersion.CSharp9)]
    [InlineData("6.0", LanguageVersion.CSharp10)]
    [InlineData("7.0", LanguageVersion.CSharp10)]
    [InlineData("8.0", LanguageVersion.CSharp10)]
    [InlineData("9.0", LanguageVersion.CSharp10)]
    public void FromDotNetVersion_ShouldReturnExpectedLanguageVersion(string versionString, LanguageVersion expectedLanguageVersion)
    {
        // Arrange
        Version version = ParseVersion(versionString);

        // Act
        LanguageVersion? result = DotNet.LangVersion.FromDotNetVersion(version);

        // Assert
        Assert.Equal(expectedLanguageVersion, result);
    }

    private static Version ParseVersion(ReadOnlySpan<char> versionString)
    {
        const string coreIdentifier = "Core";

        if (versionString.Contains(coreIdentifier.AsSpan(), StringComparison.Ordinal))
        {
            int spaceIndex = versionString.IndexOf(' ');
            if (spaceIndex != -1)
            {
                ReadOnlySpan<char> versionPart = versionString[(spaceIndex + 1)..];
                return new Version(versionPart.ToString());
            }
        }

        return new Version(versionString.ToString());
    }
}

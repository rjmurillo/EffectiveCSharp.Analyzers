using System.Collections.Immutable;
using EffectiveCSharp.Analyzers.Common;
using Microsoft.CodeAnalysis;

namespace EffectiveCSharp.Analyzers.Tests;

public class AnalyzerConfigurationHelperTests
{
    [Fact]
    public void GetConfiguredSafeMethods_ReturnsConfiguredItems()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems("CustomNamespace.CustomClass.CustomMethod, AnotherNamespace.AnotherClass.AnotherMethod");

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Equal(2, safeMethods.Count);
        Assert.Contains("CustomNamespace.CustomClass.CustomMethod", safeMethods);
        Assert.Contains("AnotherNamespace.AnotherClass.AnotherMethod", safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_ReturnsEmptyListWhenNoItemsConfigured()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems(safeItemsValue: null);

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Empty(safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_HandlesEmptyString()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems(string.Empty);

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Empty(safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_HandlesWhitespaceOnly()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems("   ");

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Empty(safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_TrimsWhitespaceAroundItems()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems("  MethodOne  ,  MethodTwo  ,  MethodThree  ");

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Equal(3, safeMethods.Count);
        Assert.Contains("MethodOne", safeMethods);
        Assert.Contains("MethodTwo", safeMethods);
        Assert.Contains("MethodThree", safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_IgnoresEmptyEntries()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems(",MethodOne,,MethodTwo,,,");

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Equal(2, safeMethods.Count);
        Assert.Contains("MethodOne", safeMethods);
        Assert.Contains("MethodTwo", safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_HandlesDuplicateEntries()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems("MethodOne,MethodTwo,MethodOne,MethodThree,MethodTwo");

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Equal(5, safeMethods.Count);
        Assert.Equal(2, safeMethods.Count(s => string.Equals(s, "MethodOne", StringComparison.Ordinal)));
        Assert.Equal(2, safeMethods.Count(s => string.Equals(s, "MethodTwo", StringComparison.Ordinal)));
        Assert.Contains("MethodThree", safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_HandlesNullGlobalOptions()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        AnalyzerOptions options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty);

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Empty(safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_HandlesMissingDiagnosticId()
    {
        // Arrange
        const string diagnosticId = "NonExistentDiagnosticId";
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems("MethodOne,MethodTwo");

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Empty(safeMethods);
    }

    [Fact]
    public void GetConfiguredSafeMethods_HandlesSpecialCharacters()
    {
        // Arrange
        const string diagnosticId = DiagnosticIds.StaticClassMemberInitialization;
        const string specialMethods = "Method$One,Method@Two,Method#Three";
        AnalyzerOptions options = CreateAnalyzerOptionsWithSafeItems(specialMethods);

        // Act
        List<string> safeMethods = AnalyzerConfigurationHelper.GetConfiguredSafeItems(options, diagnosticId);

        // Assert
        Assert.NotNull(safeMethods);
        Assert.Equal(3, safeMethods.Count);
        Assert.Contains("Method$One", safeMethods);
        Assert.Contains("Method@Two", safeMethods);
        Assert.Contains("Method#Three", safeMethods);
    }

    private static AnalyzerOptions CreateAnalyzerOptionsWithSafeItems(string? safeItemsValue)
    {
        ImmutableArray<AdditionalText> additionalFiles = [];
        TestAnalyzerConfigOptionsProvider optionsProvider = new(safeItemsValue);
        return new AnalyzerOptions(additionalFiles, optionsProvider);
    }

    private class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions;

        public TestAnalyzerConfigOptionsProvider(string? safeMethodsValue)
        {
            Dictionary<string, string> options = new(StringComparer.Ordinal);
            if (safeMethodsValue != null)
            {
                options.Add($"dotnet_diagnostic.{DiagnosticIds.StaticClassMemberInitialization}.safe_items", safeMethodsValue);
            }

            _globalOptions = new TestAnalyzerConfigOptions(options);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;

        private class TestAnalyzerConfigOptions(Dictionary<string, string> options) : AnalyzerConfigOptions
        {
            public override IEnumerable<string> Keys => options.Keys;

            public override bool TryGetValue(string key, out string value)
            {
                return options.TryGetValue(key, out value!);
            }
        }
    }
}

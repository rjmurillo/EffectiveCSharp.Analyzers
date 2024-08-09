using Xunit.Abstractions;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.FormattableStringForCultureSpecificStringsAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

#pragma warning disable IDE0028 // We cannot simply object creation on TheoryData because we need to convert from object[] to string, the way it is now is cleaner

public class FormattableStringForCultureSpecificStringsTests
{
    private readonly ITestOutputHelper _output;

    public FormattableStringForCultureSpecificStringsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<string, string> TestData()
    {
        TheoryData<string> data = new()
        {
            // This should trigger the analyzer because it implicitly uses the current culture of the machine
            """
            private readonly string _message = {|ECS0005:$"The speed of light is {SpeedOfLight:N3} km/s."|};
            """,

            // Should trigger the analyzer: property initialization
            """
            public string Message { get; set; } = {|ECS0005:$"The speed of light is {SpeedOfLight:N3} km/s."|};
            """,

            // Should trigger the analyzer: local variable assignment
            """
            public void SomeMethod()
            {
                string message = {|ECS0005:$"The speed of light is {SpeedOfLight:N3} km/s."|};
            }
            """,

            // This should not trigger the analyzer because no actual formatting has taken place
            """
            private readonly FormattableString _message = $"The speed of light is {SpeedOfLight:N3} km/s.";
            """,
        };
        return data.WithReferenceAssemblyGroups(p => p == ReferenceAssemblyCatalog.Latest);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task Analyzer(string referenceAssemblyGroup, string source)
    {
        string code = $$"""
                        public class C
                        {
                          private const double SpeedOfLight = 299_792.458;

                          {{source}}
                        }
                        """;

        _output.WriteLine(code);

        await Verifier.VerifyAnalyzerAsync(
            code,
            referenceAssemblyGroup);
    }
}

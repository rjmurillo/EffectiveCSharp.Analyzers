using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerAndCodeFixVerifier<EffectiveCSharp.Analyzers.FormattableStringForCultureSpecificStringsAnalyzer, EffectiveCSharp.Analyzers.FormattableStringForCultureSpecificStringsCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.FormattableStringForCultureSpecificStringsAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

#pragma warning disable IDE0028 // We cannot simply object creation on TheoryData because we need to convert from object[] to string, the way it is now is cleaner

public class FormattableStringForCultureSpecificStringsTests(ITestOutputHelper output)
{
    public static TheoryData<string, string> TestData()
    {
        TheoryData<string> data = new()
        {
            // This should trigger the analyzer because it implicitly uses the current culture of the machine
            """
            private readonly string _message = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
            """,

            // Should trigger the analyzer: property initialization
            """
            public string Message { get; set; } = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
            """,

            // Should trigger the analyzer: local variable assignment
            """
            public string M()
            {
                string message = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
                return message;
            }
            """,

            // This should not trigger the analyzer because no actual formatting has taken place, just instructions about the format
            """
            private readonly FormattableString _message = $"The speed of light is {SpeedOfLight:N3} km/s.";
            """,

            // This should not trigger the analyzer because strings are lowered to a string.Concat
            """
            public string M()
            {
                string h = "hello";
                string w = "world";
                return $"{h}, {w}!";
            }
            """,
            """
            private static readonly string h = "hello";
            private static readonly string w = "world";
            private static readonly string hw = $"{h}, {w}!";
            """,

            // Conditional interpolated string
            """
            private const bool condition = false;
            private readonly string? _message = condition ? {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|} : null;
            """,

            // StringBuilder
            """
            private string M()
            {
              var builder = new StringBuilder();
              builder.Append({|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|});
              return builder.ToString();
            }
            """,

            // With formatting
            """
            private readonly string _message = {|ECS0500:$"The speed of light is {SpeedOfLight,10:N3} km/s."|};
            """,

            // Nested interpolated strings
            """
            private readonly string _message = {|ECS0500:$"The speed of light is {string.Create(CultureInfo.InvariantCulture, $"{SpeedOfLight:N3}")} km/s."|};
            """,

            // Complex expressions in interpolated strings
            """
            private string M()
            {
              return $"The speed of light is {Math.Round(SpeedOfLight, 2):N2} km/s.";
            }
            """,

            // Local functions and lambdas
            """
            Func<string> lambda = () => {|ECS0500:$"The speed of light is {SpeedOfLight,10:N3} km/s."|};
            """,
        };

        // FormattableString was introduced in C# 6.0 and .NET 4.6 with extended capabilities up through C# 9.0
        // string.Create was introduced in C# 10 and .NET 6

        // REVIEW: There's a similar version of this logic in the analyzer and code fix provider as well
        return data.WithReferenceAssemblyGroups(p => ReferenceAssemblyCatalog.DotNetCore.Contains(p, StringComparer.Ordinal));
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

        output.WriteLine(code);

        await Verifier.VerifyAnalyzerAsync(
            code,
            referenceAssemblyGroup);
    }

    [Fact]
    public async Task CodeFix_Net6()
    {
        const string testCode = """
                          public class C
                          {
                            private const double SpeedOfLight = 299_792.458;
                            private readonly string _message = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
                            public string Message { get; set; } = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
                            public string M()
                            {
                              string message = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
                              return message;
                            }
                            public string S()
                            {
                              string h = "hello";
                              string w = "world";
                              return $"{h}, {w}!";
                            }
                          }
                          """;

        const string fixedCode = """
                           public class C
                           {
                             private const double SpeedOfLight = 299_792.458;
                             private readonly string _message = string.Create(CultureInfo.CurrentCulture, $"The speed of light is {SpeedOfLight:N3} km/s.");
                             public string Message { get; set; } = string.Create(CultureInfo.CurrentCulture, $"The speed of light is {SpeedOfLight:N3} km/s.");
                             public string M()
                             {
                               string message = string.Create(CultureInfo.CurrentCulture, $"The speed of light is {SpeedOfLight:N3} km/s.");
                               return message;
                             }
                             public string S()
                             {
                               string h = "hello";
                               string w = "world";
                               return $"{h}, {w}!";
                             }
                           }
                           """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Net60);
    }

    [Fact]
    public async Task Analyzer_Net462()
    {
        const string testCode = """
                          public class C
                          {
                            private const double SpeedOfLight = 299_792.458;
                            private readonly string _message = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
                            public string Message { get; set; } = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
                            public string M()
                            {
                              string message = {|ECS0500:$"The speed of light is {SpeedOfLight:N3} km/s."|};
                              return message;
                            }
                            public string S()
                            {
                              string h = "hello";
                              string w = "world";
                              return $"{h}, {w}!";
                            }
                          }
                          """;

        await Verifier.VerifyAnalyzerAsync(testCode, ReferenceAssemblyCatalog.Net462);
    }
}

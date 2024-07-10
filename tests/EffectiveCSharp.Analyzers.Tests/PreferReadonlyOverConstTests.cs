using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerAndCodeFixVerifier<EffectiveCSharp.Analyzers.PreferReadonlyOverConstAnalyzer, EffectiveCSharp.Analyzers.PreferReadonlyOverConstCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.PreferReadonlyOverConstAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class PreferReadonlyOverConstTests
{
    public static IEnumerable<object[]> TestData()
    {
        return new object[][]
        {
            // This should not fire because it's a readonly field
            ["""public static readonly int StartValue = 5;"""],

            // This should fire because a const
            ["""{|ECS0002:public const int EndValue = 10;|}"""],

            // This should not fire because it's suppressed
            [
                """
                #pragma warning disable ECS0002
                public const int EndValue = 10;
                #pragma warning restore ECS0002
                """
            ],
        }.WithReferenceAssemblyGroups();
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task Analyzer(string referenceAssemblyGroup, string source)
    {
        await Verifier.VerifyAnalyzerAsync(
            $$"""
              public class UsefulValues
              {
                  {{source}}
              }
              """,
            referenceAssemblyGroup);
    }

    [Fact]
    public async Task CodeFix()
    {
        string testCode = """
                          class Program
                          {
                              {|ECS0002:private const int StartValue = 5;|}
                          }
                          """;

        string fixedCode = """
                           class Program
                           {
                               private static readonly int StartValue = 5;
                           }
                           """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Net80);
    }
}

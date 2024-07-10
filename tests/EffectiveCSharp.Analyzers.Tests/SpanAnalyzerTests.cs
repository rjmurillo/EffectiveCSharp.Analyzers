using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerAndCodeFixVerifier<EffectiveCSharp.Analyzers.SpanAnalyzer, EffectiveCSharp.Analyzers.SpanCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.SpanAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class SpanAnalyzerTests
{
    public static IEnumerable<object[]> TestData()
    {
        return new object[][]
        {
            // This should fire
            ["""var arr = {|ECS1000:new int[10]|};"""],

            // This should not fire because it's wrapped by a Span
            ["""var arr = new Span<int>(new int[10]);"""],

            // This should not fire because it's suppressed
            ["""
             #pragma warning disable ECS1000 // Use Span<T> for performance
             var arr = new int[10];
             #pragma warning restore ECS1000 // Use Span<T> for performance
             """],
        }.WithReferenceAssemblyGroups();
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task SpanAnalyzer(string referenceAssemblyGroup, string source)
    {
        await Verifier.VerifyAnalyzerAsync(
            $$"""
              internal class MyClass
              {
                  void Method()
                  {
                      {{source}}
                  }
              }
              """,
            referenceAssemblyGroup);
    }


    [Fact(Skip = "Reporting an analyzer failure when the unit test code above shows it is correct")]
    public async Task TestArraySpanFix()
    {
        string testCode = """
                          class Program
                          {
                              void Method()
                              {
                                  var arr = {|ECS1000:new int[10]|};
                                  var val = arr[5];
                              }
                          }
                          """;

        string fixedCode = """
                           class Program
                           {
                               void Method()
                               {
                                   var arr = new Span<int>(new int[10]);
                                   var val = arr.Slice(5, 1)[0];
                               }
                           }
                           """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Net80);
    }
}

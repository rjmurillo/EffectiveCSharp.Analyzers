using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerAndCodeFixVerifier<EffectiveCSharp.Analyzers.SpanAnalyzer, EffectiveCSharp.Analyzers.SpanCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.SpanAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class SpanAnalyzerTests
{
    public static IEnumerable<object[]> TestData()
    {
        return new object[][]
        {
            ["""var arr = {|ECS1000:new int[10]|};"""],
            ["""var arr = new Span<int>(new int[10]);"""],
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

    [Fact]
    public async Task TestArraySpanFix()
    {
        string testCode = """
                          using System;

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
                           using System;

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

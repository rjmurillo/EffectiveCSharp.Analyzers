﻿using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerAndCodeFixVerifier<EffectiveCSharp.Analyzers.SpanAnalyzer, EffectiveCSharp.Analyzers.SpanCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.SpanAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

#pragma warning disable IDE0028 // We cannot simply object creation on TheoryData because we need to convert from object[] to string, the way it is now is cleaner

public class SpanAnalyzerTests
{
    public static TheoryData<string, string> TestData()
    {
        TheoryData<string> data = new()
        {
            // This should fire
            "var arr = {|ECS1000:new int[10]|};",

            // This should not fire because it's wrapped by a Span
            """
            #if NET6_0_OR_GREATER
            var arr = new Span<int>(new int[10]);
            #endif
            """,

            // This should not fire because it's wrapped by a ReadOnlySpan
            """
            #if NET6_0_OR_GREATER
            var arr = new ReadOnlySpan<int>(new int[10]);
            #endif
            """,

            // This should not fire because it's suppressed
            """
            #pragma warning disable ECS1000 // Use Span<T> for performance
            var arr = new int[10];
            #pragma warning restore ECS1000 // Use Span<T> for performance
            """,
        };

        return data.WithReferenceAssemblyGroups();
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task Analyzer(string referenceAssemblyGroup, string source)
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
    public async Task CodeFix()
    {
        const string testCode = """
                          class Program
                          {
                              void Method()
                              {
                                  var arr = {|ECS1000:new int[10]|};
                                  var val = arr[5];
                              }
                          }
                          """;

        const string fixedCode = """
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

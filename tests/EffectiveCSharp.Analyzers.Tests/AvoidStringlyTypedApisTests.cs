using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerAndCodeFixVerifier<EffectiveCSharp.Analyzers.AvoidStringlyTypedApisAnalyzer, EffectiveCSharp.Analyzers.AvoidStringlyTypedApisCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.AvoidStringlyTypedApisAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

#pragma warning disable IDE0028 // We cannot simply object creation on TheoryData because we need to convert from object[] to string, the way it is now is cleaner

public class AvoidStringlyTypedApisTests
{
    public static TheoryData<string, string> TestData()
    {
        TheoryData<string> data = new()
        {
            // This should not fire because it's using nameof
            "nameof(thisCantBeNull)",

            // This should fire because it's referring to a member name using a string literal
            """
                {|ECS0600:"thisCantBeNull"|}
            """,

            // This should not fire because it's suppressed
            """
            #pragma warning disable ECS0600
            "thisCantBeNull"
            #pragma warning restore ECS0600
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
              public class MyClass
              {
                public static void ExceptionMessage(object thisCantBeNull)
                {
                    if (thisCantBeNull == null)
                    {
                        throw new ArgumentNullException(
                            {{source}}
                            ,
                            "We told you this cant be null");
                    }
                }
              }
              """,
            referenceAssemblyGroup);
    }

    [Fact]
    public async Task CodeFix()
    {
        const string testCode = """
                                public class MyClass
                                {
                                  public static void ExceptionMessage(object thisCantBeNull)
                                  {
                                      if (thisCantBeNull == null)
                                      {
                                          throw new ArgumentNullException(
                                              {|ECS0600:"thisCantBeNull"|},
                                              "We told you this cant be null");
                                      }
                                  }
                                }
                                """;

        const string fixedCode = """
                                 public class MyClass
                                 {
                                   public static void ExceptionMessage(object thisCantBeNull)
                                   {
                                       if (thisCantBeNull == null)
                                       {
                                           throw new ArgumentNullException(
                                               nameof(thisCantBeNull),
                                               "We told you this cant be null");
                                       }
                                   }
                                 }
                                 """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Net80);
    }
}

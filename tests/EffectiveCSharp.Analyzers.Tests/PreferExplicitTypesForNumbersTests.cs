using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerAndCodeFixVerifier<EffectiveCSharp.Analyzers.PreferExplicitTypesOnNumbersAnalyzer, EffectiveCSharp.Analyzers.PreferExplicitTypesForNumbersCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.PreferExplicitTypesOnNumbersAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class PreferExplicitTypesForNumbersTests
{
    [Fact]
    public async Task Analyzer()
    {
        // There are five outputs to the following:
        // Declared Type: Double, Value = 166.666666666667
        // Declared Type: Single, Value = 166.6667
        // Declared Type: Decimal, Value = 166.66666666666666666666666667
        // Declared Type: Int32, Value = 166
        // Declared Type: Int64, Value = 166
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              public void MyMethod()
              {
                {|ECS0001:var f = GetMagicNumber();|}
                {|ECS0001:var total = 100 * f / 6;|}
                Console.WriteLine($"Declared Type:{total.GetType().Name}, Value:{total}");
              }

              double GetMagicNumber() => 100.0;
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task CodeFix()
    {
        const string testCode = """
                                public class MyClass
                                {
                                  public void MyMethod()
                                  {
                                    {|ECS0001:var f = GetMagicNumber();|}
                                    {|ECS0001:var total = 100 * f / 6;|}
                                    Console.WriteLine($"Declared Type:{total.GetType().Name}, Value:{total}");
                                  }

                                  double GetMagicNumber() => 100.0;
                                }
                                """;

        const string fixedCode = """
                                 public class MyClass
                                 {
                                   public void MyMethod()
                                   {
                                     double f = GetMagicNumber();
                                     double total = 100 * f / 6;
                                     Console.WriteLine($"Declared Type:{total.GetType().Name}, Value:{total}");
                                   }

                                   double GetMagicNumber() => 100.0;
                                 }
                                 """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Latest);
    }
}

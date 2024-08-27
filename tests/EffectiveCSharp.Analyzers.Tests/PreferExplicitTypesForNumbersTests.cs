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
                {|ECS0100:var f = GetMagicNumber();|}
                {|ECS0100:var total = 100 * f / 6;|}
                Console.WriteLine($"Declared Type:{total.GetType().Name}, Value:{total}");
              }

              double GetMagicNumber() => 100.0;
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerWithInlineFunctionCall()
    {
        // It's not the use of `var` that causes the problem. The cause is that it's not
        // clear from reading the code which type is returned by GetMagicNumber() and which
        // built-in conversions may be in play. The same problems occur when the variable
        // `f` is removed from the method
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              public void MyMethod()
              {
                {|ECS0100:var total = 100 * GetMagicNumber() / 6;|}
                Console.WriteLine($"Declared Type:{total.GetType().Name}, Value:{total}");
              }

              double GetMagicNumber() => 100.0;
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerWithInlineFunctionCallOfDifferentType()
    {
        // It also doesn't matter if you explicitly declare the type for `total`
        // The type of total is `double`, but the result may still be rounded
        // if GetMagicNumber returns an integer value.
        //
        // In this case, we don't have a `var` to capture
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              public void MyMethod()
              {
                double total = {|ECS0100:100 * GetMagicNumber() / 6|};
                Console.WriteLine($"Declared Type:{total.GetType().Name}, Value:{total}");
              }

              int GetMagicNumber() => 100;
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
                                    {|ECS0100:var f = GetMagicNumber();|}
                                    {|ECS0100:var total = 100 * f / 6;|}
                                    Console.WriteLine($"Declared Type:{total.GetType().Name}, Value:{total}");
                                  }

                                  decimal GetMagicNumber() => 100.0M;
                                }
                                """;

        const string fixedCode = """
                                 public class MyClass
                                 {
                                   public void MyMethod()
                                   {
                                     decimal f = GetMagicNumber();
                                     decimal total = 100 * f / 6;
                                     Console.WriteLine($"Declared Type:{total.GetType().Name}, Value:{total}");
                                   }

                                   decimal GetMagicNumber() => 100.0M;
                                 }
                                 """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Latest);
    }
}

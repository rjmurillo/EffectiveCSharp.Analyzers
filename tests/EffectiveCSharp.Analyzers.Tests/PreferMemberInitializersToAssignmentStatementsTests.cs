using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.PreferMemberInitializersToAssignmentStatementsAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class PreferMemberInitializersToAssignmentStatementsTests
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
              private string labels;

              public MyClass()
              {
                {|ECS0012:labels = string.Empty;|}
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }
}

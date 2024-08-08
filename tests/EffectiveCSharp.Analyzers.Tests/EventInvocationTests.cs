using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.EventInvocationAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

#pragma warning disable SA1204  // Static members are grouped with their Theory
#pragma warning disable SA1001  // The harness has literal code as a string, which can be weirdly formatted
#pragma warning disable SA1113
#pragma warning disable S125    // There's code in comments as examples
#pragma warning disable MA0051  // Some test methods are "too long"
#pragma warning disable MA0007  // There are multiple types of tests defined in theory data
#pragma warning disable IDE0028 // We cannot simply object creation on TheoryData because we need to convert from object[] to string, the way it is now is cleaner
#pragma warning disable AsyncFixer01


public class EventInvocationTests
{
    public static TheoryData<string, string> TestData()
    {
        TheoryData<string> data = new()
        {
            // When no event handlers have been attached to the Updated event
            // the code will throw NRE
            "Updated(this, counter);",

            // Developers need to wrap any event invocation in a check
            // This code has a bug: the null check can pass but the event
            // can be unsubscribed by another thread, and you still get NRE
            """
            {|ECS0008:if (Updated != null)
                Updated(this, counter);|}
            """,

            // This code is correct in earlier versions of C#
            """
            var handler = Updated;
            {|ECS0008:if (handler != null)
                handler(this, counter);|}
            """,

            // Can be simplified to a single line with Null conditional operators reduce cognitive overhead
            "Updated?.Invoke(this, counter);",
        };

#pragma warning disable MA0002 // IEqualityComparer<string> or IComparer<string> is missing
        return data.WithReferenceAssemblyGroups(p => ReferenceAssemblyCatalog.DotNetCore.Contains(p));
#pragma warning restore MA0002 // IEqualityComparer<string> or IComparer<string> is missing
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task Analyzer(string referenceAssemblyGroup, string code)
    {
        await Verifier.VerifyAnalyzerAsync(
            $$"""
            public class EventSource
            {
              private EventHandler<int> Updated;
              private int counter;

              public void RaiseUpdates()
              {
                counter++;
                {{code}}
              }
            }
            """,
            referenceAssemblyGroup);
    }
}

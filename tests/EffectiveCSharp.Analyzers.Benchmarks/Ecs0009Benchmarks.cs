namespace EffectiveCSharp.Analyzers.Benchmarks;

[InProcess]
[MemoryDiagnoser]
public class Ecs0009Benchmarks
{
    private static CompilationWithAnalyzers? BaselineCompilation { get; set; }

    private static CompilationWithAnalyzers? TestCompilation { get; set; }

    [IterationSetup]
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Async setup not supported in BenchmarkDotNet.See https://github.com/dotnet/BenchmarkDotNet/issues/2442.")]
    public static void SetupCompilation()
    {
        List<(string Name, string Content)> sources = [];
        for (int index = 0; index < Constants.NumberOfCodeFiles; index++)
        {
            string name = $"TypeName{index}";
            sources.Add((name, $$"""

using System;

internal class {{name}}
{
    public void Method()
    {
        int i = 1;
        object o = i; // boxing

        i = (int)o; // unboxing

        int firstNumber = 4;
        int secondNumber = 2;
        int thirdNumber = 6;

        Method(
            "A few numbers: {0}, {1}, {2}",
            firstNumber,
            secondNumber,
            thirdNumber);

        // Using the Person in a collection
        var attendees = new List<Person>();
        var p = new Person { Name = "Old Name" };
        attendees.Add(p);

        // Try to change the name
        var p2 = attendees[0];
        p2.Name = "New Name";

        // Writes "Old Name":
        Console.WriteLine(attendees[0].ToString());
    }

    void Method(params object?[]? arg) { }
}

public struct Person
{
  public string Name { get; set; }
  public override string ToString() => Name;
}

"""));
        }

        (BaselineCompilation, TestCompilation) =
            BenchmarkCSharpCompilationFactory
            .CreateAsync<MinimizeBoxingUnboxingAnalyzer>(sources.ToArray())
            .GetAwaiter()
            .GetResult();
    }

    [Benchmark]
    public async Task Ecs0009WithDiagnostics()
    {
        ImmutableArray<Diagnostic> diagnostics =
            (await TestCompilation!
            .GetAnalysisResultAsync(CancellationToken.None)
            .ConfigureAwait(false))
            .AssertValidAnalysisResult()
            .GetAllDiagnostics();

        // We have 4 instances in our test sample
        if (diagnostics.Length != Constants.NumberOfCodeFiles * 5)
        {
            throw new InvalidOperationException($"Expected '{Constants.NumberOfCodeFiles:N0}' analyzer diagnostics but found '{diagnostics.Length:N0}'");
        }
    }

    [Benchmark(Baseline = true)]
    public async Task Ecs0009Baseline()
    {
        ImmutableArray<Diagnostic> diagnostics =
            (await BaselineCompilation!
            .GetAnalysisResultAsync(CancellationToken.None)
            .ConfigureAwait(false))
            .AssertValidAnalysisResult()
            .GetAllDiagnostics();

        if (diagnostics.Length != 0)
        {
            throw new InvalidOperationException($"Expected no analyzer diagnostics but found '{diagnostics.Length}'");
        }
    }
}

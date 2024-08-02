namespace EffectiveCSharp.Analyzers.Benchmarks;

[InProcess]
[MemoryDiagnoser]
public class Ecs0004Benchmarks
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
            sources.Add((name, @$"
using System;

public class {name}
{{
  public void Main()
  {{
      var str = string.Format(""The value of pi is {{0}}"", Math.PI.ToString(""F2""));
  }}
}}
"));
        }

        (BaselineCompilation, TestCompilation) =
            BenchmarkCSharpCompilationFactory
            .CreateAsync<ReplaceStringFormatAnalyzer>(sources.ToArray())
            .GetAwaiter()
            .GetResult();
    }

    [Benchmark]
    public async Task Ecs0004WithDiagnostics()
    {
        ImmutableArray<Diagnostic> diagnostics =
            (await TestCompilation!
            .GetAnalysisResultAsync(CancellationToken.None)
            .ConfigureAwait(false))
            .AssertValidAnalysisResult()
            .GetAllDiagnostics();

        if (diagnostics.Length != Constants.NumberOfCodeFiles)
        {
            throw new InvalidOperationException($"Expected '{Constants.NumberOfCodeFiles:N0}' analyzer diagnostics but found '{diagnostics.Length}'");
        }
    }

    [Benchmark(Baseline = true)]
    public async Task Ecs0004Baseline()
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

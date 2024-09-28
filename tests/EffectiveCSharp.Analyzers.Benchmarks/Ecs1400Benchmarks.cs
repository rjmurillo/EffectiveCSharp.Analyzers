using System.Globalization;
using BenchmarkDotNet.Diagnostics.dotTrace;

namespace EffectiveCSharp.Analyzers.Benchmarks;

[InProcess]
[MemoryDiagnoser]
[DotTraceDiagnoser]
public class Ecs1400Benchmarks
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
            string name = "TypeName" + index.ToString(CultureInfo.InvariantCulture);
            sources.Add((name, @$"
                                public class {name}
                                {{
                                    private int _value;

                                    public {name}()
                                    {{
                                        _value = 10;
                                    }}

                                    public {name}(int value)
                                    {{
                                        _value = 10;
                                    }}
                                }}"));
        }

        (BaselineCompilation, TestCompilation) =
            BenchmarkCSharpCompilationFactory
            .CreateAsync<MinimizeDuplicateInitializationLogicAnalyzer>(sources.ToArray())
            .GetAwaiter()
            .GetResult();
    }

    [Benchmark]
    public async Task Ecs1400WithDiagnostics()
    {
        ImmutableArray<Diagnostic> diagnostics =
            (await TestCompilation!
            .GetAnalysisResultAsync(CancellationToken.None)
            .ConfigureAwait(false))
            .AssertValidAnalysisResult()
            .GetAllDiagnostics();

        if (diagnostics.Length != Constants.NumberOfCodeFiles * 2)
        {
            throw new InvalidOperationException($"Expected '{Constants.NumberOfCodeFiles * 2:N0}' analyzer diagnostics but found '{diagnostics.Length:N0}'");
        }
    }

    [Benchmark(Baseline = true)]
    public async Task Ecs1400Baseline()
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

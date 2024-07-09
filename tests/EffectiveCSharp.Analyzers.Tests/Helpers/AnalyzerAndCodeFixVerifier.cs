namespace EffectiveCSharp.Analyzers.Tests.Helpers;

internal static class AnalyzerAndCodeFixVerifier<TAnalyzer, TCodeFixProvider>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFixProvider : CodeFixProvider, new()
{
    public static async Task VerifyCodeFixAsync(string testCode, string fixedCode, string referenceAssemblyGroup)
    {
        ReferenceAssemblies referenceAssemblies = ReferenceAssemblyCatalog.Catalog[referenceAssemblyGroup];

        await new Test<TAnalyzer, TCodeFixProvider>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ReferenceAssemblies = referenceAssemblies,
        }.RunAsync().ConfigureAwait(false);
    }
}

﻿namespace EffectiveCSharp.Analyzers.Tests.Helpers;

internal static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static async Task VerifyAnalyzerAsync(string source) => await VerifyAnalyzerAsync(source, ReferenceAssemblyCatalog.Latest);

    public static async Task VerifyAnalyzerAsync(string source, string referenceAssemblyGroup)
    {
        ReferenceAssemblies referenceAssemblies = ReferenceAssemblyCatalog.Catalog[referenceAssemblyGroup];

        await new Test<TAnalyzer, EmptyCodeFixProvider>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = referenceAssemblies,
        }.RunAsync().ConfigureAwait(false);
    }
}

using Microsoft.CodeAnalysis.CSharp.Testing;

namespace EffectiveCSharp.Analyzers.Tests.Helpers;

/// <summary>
/// An implementation of <see cref="CSharpCodeFixTest{TAnalyzer, TCodeFixProvider, TVerifier}"/> that sets default configuration
/// for our tests.
/// </summary>
/// <typeparam name="TAnalyzer">The type of analyzer to test.</typeparam>
/// <typeparam name="TCodeFixProvider">The type of code fix provider to test. If the test is for an analyzer without a code fix, use <see cref="EmptyCodeFixProvider"/>.</typeparam>
internal class Test<TAnalyzer, TCodeFixProvider> : CSharpCodeFixTest<TAnalyzer, TCodeFixProvider, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFixProvider : CodeFixProvider, new()
{
    public Test()
    {
        // Add common usings to all test cases to avoid test authoring errors.
        const string globalUsings =
            """
            global using global::System;
            global using global::System.Collections.Generic;
            global using global::System.Globalization;
            global using global::System.IO;
            global using global::System.Linq;
            global using global::System.Text;
            global using global::System.Net.Http;
            global using global::System.Threading;
            global using global::System.Threading.Tasks;
            """;

        TestState.Sources.Add(globalUsings);
        FixedState.Sources.Add(globalUsings);

        MarkupOptions = MarkupOptions.UseFirstDescriptor;
    }
}

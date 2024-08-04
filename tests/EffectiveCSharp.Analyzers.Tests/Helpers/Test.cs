﻿using Microsoft.CodeAnalysis.CSharp.Testing;

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
            global using System;
            global using System.Collections.Generic;
            global using System.Linq;
            """;

        TestState.Sources.Add(globalUsings);
        FixedState.Sources.Add(globalUsings);

        MarkupOptions = MarkupOptions.UseFirstDescriptor;
    }
}

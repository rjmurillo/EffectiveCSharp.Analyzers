﻿using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.MinimizeBoxingUnboxingAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

#pragma warning disable SA1204  // Static members are grouped with their Theory

public class AvoidBoxingUnboxingTests
{
    public static IEnumerable<object[]> TestData()
    {
        return new object[][]
        {
            // This should fire
            [
                """
                int i = 5;
                object o = {|ECS0009:i|}; // boxing
                """
            ],

            // This should fire
            [
                """
                int i = 5;
                object o = {|ECS0009:(object)i|}; // boxing
                """
            ],

            // This should fire for method call with value type defined in System.Object
            [
                """
                int i = 5;
                Console.WriteLine(i); // boxing
                """
            ],

            // This should not fire because it's suppressed
            [
                """
                #pragma warning disable ECS0009 // Minimize boxing and unboxing
                int i = 5;
                object o = i; // boxing
                #pragma warning restore ECS0009 // Minimize boxing and unboxing
                """
            ],

            [
                """
                var dt = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
                """
                ],
        }.WithReferenceAssemblyGroups();
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task Analyzer(string referenceAssemblyGroup, string source)
    {
        await Verifier.VerifyAnalyzerAsync(
            $$"""
              internal class MyClass
              {
                  void Method()
                  {
                      {{source}}
                  }
              }
              """,
            referenceAssemblyGroup);
    }

    public static IEnumerable<object[]> TestData2()
    {
        return new object[][]
        {
            // This should fire
            // Equal to
            // Method("a few number...", new object[3]{ (object)firstNumber... });
            [
                """
                void Foo()
                {
                  int firstNumber = 4;
                  int secondNumber = 2;
                  int thirdNumber = 6;

                  Method(
                   "A few numbers: {0}, {1}, {2}",
                   {|ECS0009:firstNumber|},
                   {|ECS0009:secondNumber|},
                   {|ECS0009:thirdNumber|}
                   );
                }
                """
            ],

            // This should not fire because it's suppressed
            [
                """
                void Foo()
                {
                  int firstNumber = 4;
                  int secondNumber = 2;
                  int thirdNumber = 6;

                  Method(
                   "A few numbers: {0}, {1}, {2}",
                   #pragma warning disable ECS0009 // Minimize boxing and unboxing
                   firstNumber,
                   secondNumber,
                   thirdNumber
                   #pragma warning restore ECS0009 // Minimize boxing and unboxing
                   );
                }
                """
            ],

            // This should not fire because the string interpolation does not box
            [
                """
                void Foo()
                {
                  int firstNumber = 4;
                  int secondNumber = 2;
                  int thirdNumber = 6;

                  Method(
                   $"A few numbers: {firstNumber}, {secondNumber}, {thirdNumber}"
                   );
                }
                """
            ],

            [
                """
                private const int MyNumber = 42;

                void Foo()
                {
                  for(var i = 0; i <= MyNumber; i++)
                  {
                    Method("Bar");
                  }
                }
                """
                ],

            // Regression test: we are too aggressive when assigning ctor params to read-only properties
            [
            """
            public int Arg { get; }

            public MyClass(int arg)
            {
                Arg = arg;
            }
            """
                    ],
        }.WithReferenceAssemblyGroups();
    }

    [Theory]
    [MemberData(nameof(TestData2))]
    public async Task AnalyzerParams(string referenceAssemblyGroup, string source)
    {
        await Verifier.VerifyAnalyzerAsync(
            $$"""
              internal class MyClass
              {
                  {{source}}

                  void Method(string arg) { }

                  void Method(params object?[]? arg) { }
              }
              """,
            referenceAssemblyGroup);
    }

    [Fact]
    public async Task AnalyzerRecordType()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
using System;

namespace MyNamespace;

public record MyRecord(bool Flag);

public class MyClass
{
  public void Method(MyRecord rec)
  {
    if (rec.Flag)
    {
      Console.WriteLine("Flag is true");
    }
  }
}
""",
            ReferenceAssemblyCatalog.Net80);
    }
}

using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.PreferMemberInitializersToAssignmentStatementsAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class PreferMemberInitializersToAssignmentStatementsTests
{
    [Fact]
    public async Task AnalyzerSimpleCase()
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

    [Fact]
    public async Task AnalyzerShouldNotFireSinceFieldAlreadyInitializedInDeclaration()
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
              private List<string> listOfStrings = new List<string>();

              public MyClass()
              {
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldNotFireSinceInStructs()
    {
        // There are five outputs to the following:
        // Declared Type: Double, Value = 166.666666666667
        // Declared Type: Single, Value = 166.6667
        // Declared Type: Decimal, Value = 166.66666666666666666666666667
        // Declared Type: Int32, Value = 166
        // Declared Type: Int64, Value = 166
        await Verifier.VerifyAnalyzerAsync(
            """
            public struct MyStruct
            {
              public double MyDouble;
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireWhenInitToZeroOrNull()
    {
        // There are five outputs to the following:
        // Declared Type: Double, Value = 166.666666666667
        // Declared Type: Single, Value = 166.6667
        // Declared Type: Decimal, Value = 166.66666666666666666666666667
        // Declared Type: Int32, Value = 166
        // Declared Type: Int64, Value = 166
        await Verifier.VerifyAnalyzerAsync(
            """
            public struct MyStruct
            {
              public double MyDouble;
            }

            public class MyClass
            {
              {|ECS0012:private MyStruct structInstance = new MyStruct();|}

              public MyClass()
              {
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireDueToMissingDeclaration()
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
              {|ECS0012:private List<string> listOfString;|}

              public MyClass()
              {
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireDueToInitializigToZero()
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
              {|ECS0012:private double mynum = 0;|}

              public MyClass()
              {
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireWhenBeingInitializedInFieldAndConstructorsWith()
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
              private List<string> listOfString = new List<string>();

              public MyClass()
              {
                {|ECS0012:listOfString = new List<string>();|}
              }

              public MyClass(int size)
              {
                {|ECS0012:listOfString = new List<string>();|}
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireWhenBeingInitializedInFieldAndConstructorsWithArgUse()
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
              {|ECS0012:private List<string> listOfString = new List<string>();|}

              public MyClass()
              {
                listOfString = new List<string>();
              }

              public MyClass(int size)
              {
                listOfString = new List<string>(size);
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }
}

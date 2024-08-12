using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.CodeFixVerifier<EffectiveCSharp.Analyzers.PreferMemberInitializersToAssignmentStatementsAnalyzer, EffectiveCSharp.Analyzers.PreferMemberInitializersToAssignmentStatementsCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.PreferMemberInitializersToAssignmentStatementsAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class PreferMemberInitializersToAssignmentStatementsTests
{
    [Fact]
    public async Task AnalyzerSimpleCase()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string labels;

              public MyClass()
              {
                {|ECS1200:labels = string.Empty;|}
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldNotFireSinceFieldAlreadyInitializedInDeclaration()
    {
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
    public async Task AnalyzerShouldNotFireSinceInWithZeroable()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              public double MyDouble;
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldNotFireSinceInWithNullable()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              public string? MyString;
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireWhenInitToZeroOrNull()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public struct MyStruct
            {
              public double MyDouble;
            }

            public class MyClass
            {
              {|ECS1201:private MyStruct structInstance = new MyStruct();|}

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
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              {|ECS1203:private List<string> listOfString;|}

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
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              {|ECS1201:private double mynum = 0;|}

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
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private List<string> listOfString = new List<string>();

              public MyClass()
              {
                {|ECS1200:listOfString = new List<string>();|}
              }

              public MyClass(int size)
              {
                {|ECS1200:listOfString = new List<string>();|}
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireWhenBeingInitializedInFieldAndConstructorsWithArgUse()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              {|ECS1202:private List<string> listOfString = new List<string>();|}

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

    [Fact]
    public async Task CodeFixMissingInitialization()
    {
        const string testCode =
        """
          public class MyClass
          {
            {|ECS1203:private List<string> listOfString;|}

            public MyClass()
            {
            }
          }
        """;

        const string fixedCode =
        """
          public class MyClass
          {
            private List<string> listOfString = new List<string>();

            public MyClass()
            {
            }
          }
        """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task CodeFixInitializedToNullOrZero()
    {
        const string testCode =
        """
          public class MyClass
          {
            {|ECS1201:private string? myString = null;|}

            public MyClass()
            {
            }
          }
        """;

        const string fixedCode =
        """
          public class MyClass
          {
            private string? myString;

            public MyClass()
            {
            }
          }
        """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task CodeFixDivergingInitializations()
    {
        const string testCode =
        """
          public class MyClass
          {
            {|ECS1202:private List<string> listOfString = new List<string>();|}

            public MyClass()
            {
              listOfString = new List<string>();
            }

            public MyClass(int size)
            {
              listOfString = new List<string>(size);
            }
          }
        """;

        const string fixedCode =
        """
          public class MyClass
          {
            private List<string> listOfString;

            public MyClass()
            {
              listOfString = new List<string>();
            }

            public MyClass(int size)
            {
              listOfString = new List<string>(size);
            }
          }
        """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task CodeFixInitializeInDeclarationWhenIdenticalInitializations()
    {
        const string testCode =
        """
          public class MyClass
          {
            private List<string> listOfString = new List<string>();

            public MyClass()
            {
              {|ECS1200:listOfString = new List<string>();|}
            }

            public MyClass(int size)
            {
              {|ECS1200:listOfString = new List<string>();|}
            }
          }
        """;

        const string fixedCode =
        """
          public class MyClass
          {
            private List<string> listOfString = new List<string>();

            public MyClass()
            {
            }

            public MyClass(int size)
            {
            }
          }
        """;

        await CodeFixVerifier.VerifyCodeFixAsync(testCode, fixedCode, ReferenceAssemblyCatalog.Net80);
    }
}

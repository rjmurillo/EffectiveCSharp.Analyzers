using CodeFixVerifier = EffectiveCSharp.Analyzers.Tests.Helpers.CodeFixVerifier<EffectiveCSharp.Analyzers.PreferDeclarationInitializersToAssignmentStatementsAnalyzer, EffectiveCSharp.Analyzers.PreferDeclarationInitializersToAssignmentStatementsCodeFixProvider>;
using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.PreferDeclarationInitializersToAssignmentStatementsAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class PreferDeclarationInitializersToAssignmentStatementsTests
{
    [Fact]
    public async Task FieldsInitializedWithDefaultKeyword()
    {
        // Fields initialized with the `default` keyword
        // Ensure that such initializations are correctly handled and not flagged
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private int value = default;
              private MyClass instance = default;
              
              public MyClass()
              {
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsInitializedInNestedTypes()
    {
        // Fields within nested classes or structs are initialized in their own ctors or the parent class
        // The analyzer should recognize this case and should flag for declaration initialization
        await Verifier.VerifyAnalyzerAsync(
            """
            public class Outer
            {
              private string outerLabel;
              {|ECS1202:private InnerClass inner = new() { innerLabel = "Inner" };|}
            
              public Outer()
              {
                outerLabel = {|ECS1200:"Outer"|};
                inner = new InnerClass { innerLabel = outerLabel };
              }
            
              public class InnerClass
              {
                internal string innerLabel;
            
                public InnerClass()
                {
                  innerLabel = {|ECS1200:"Inner"|};
                }
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsInitializedWithPropertySetters()
    {
        // Fields are initialized within property setters rater than directly or with ctors
        // The analyzer should recognize this case and should not flag for declaration initialization
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string label;
              
              public string Label
              {
                get => label;
                set => label = value;
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsWithCtorInitializationPartialClass()
    {
        // Fields in partial classes that are initialized in different parts of the class.
        // Ensure the analyzer correctly handles partial classes and fields initialized across them.
        await Verifier.VerifyAnalyzerAsync(
            """
            public partial class MyClass
            {
              {|ECS1203:private string label;|}
            }
            
            public partial class MyClass
            {  
              public MyClass()
              {
                label = "Initialized in partial class ctor";
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsWithNoInitialization()
    {
        // Fields are not initialized either in the declaration or the ctor
        // The analyzer suggests adding an initializer in such cases
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              {|ECS1203:private string label;|}
              
              public MyClass()
              {
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsInitializedBasedOnExternalParametersOrData()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string label;
              
              public MyClass(string label)
              {
                this.label = label;
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task InitializationWithCtorOverloadChains()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string label;
              
              public MyClass() : this("Default Label")
              {
              }
              
              public MyClass(string label)
              {
                this.label = label;
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsAssignedWithExpressionsDependentOnOtherFieldsWithStaticAndInterpolation()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private static readonly string prefix;
              private string label = $"{prefix} Label";
              
              static MyClass()
              {
                prefix = {|ECS1200:"Prefix"|};
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsAssignedWithExpressionsDependentOnOtherFields()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string prefix;
              private string label;
              
              public MyClass()
              {
                prefix = {|ECS1200:"Prefix"|};
                label = {|ECS1200:prefix + " Label"|};
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldInitializedWithMethodIndirectly()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string label;
              
              public MyClass()
              {
                EnsureInitialized();
              }
              
              private void EnsureInitialized()
              {
                label = InitializeLabel();
              }
              
              private string InitializeLabel()
              {
                return "Initialized by method";
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldInitializedWithMethodDirectly()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string label;
              
              public MyClass()
              {
                label = InitializeLabel();
              }
              
              private string InitializeLabel()
              {
                return "Initialized by method";
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task ObjectInitializers()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private List<string> listOfStrings = new List<string> { "Value1", "Value2" };
              
              public MyClass()
              {
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task ConstantField()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private const int val = 42;
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task ReadonlyField()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private readonly string label;
              
              public MyClass()
              {
                label = {|ECS1200:"Initialized readonly in ctor"|};
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task StaticCtor()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private static string label;
              
              static MyClass()
              {
                label = {|ECS1200:"Initialized in static ctor"|};
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsInitializedWithTernaryOperator()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string label;
              
              public MyClass(bool condition)
              {
                  label = condition ? "True" : "False";
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task FieldsInitializedWithBranchingLogic()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private string label;
              
              public MyClass(bool condition)
              {
                  if (condition)
                  {
                    label = "Conditional";
                  }
                  else
                  {
                    label = "Default";
                  }
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task MultipleVariablesInSingleDeclaration()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private int a = 1, b = 2;
            
              public MyClass()
              {
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

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
                labels = {|ECS1200:string.Empty|};
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
    public async Task AnalyzerShouldFireWhenUsingLocalVariableInConstructorWithNoArgs()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private double mynum;

              public MyClass()
              {
                double local = 5;
                mynum = {|ECS1200:local|};
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireNotWhenUsingLocalVariableInConstructorWithArgs()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private double mynum;

              public MyClass(double size)
              {
                double local = size;
                mynum = local;
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldFireWhenBeingInitializedInFieldAndConstructorsWithSameAssignments()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              private List<string> listOfString = new List<string>();

              public MyClass()
              {
                listOfString = {|ECS1200:new List<string>()|};
              }

              public MyClass(int size)
              {
                listOfString = {|ECS1200:new List<string>()|};
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task AnalyzerShouldNotFireWhenBeingInitializedInFieldAndConstructorsWithDivergingImplementations()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              {|ECS1202:private List<string> listOfString = new List<string>();|}

              public MyClass(int size)
              {
                listOfString = new List<string>(5);
              }

              public MyClass()
              {
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
              listOfString = {|ECS1200:new List<string>()|};
            }

            public MyClass(int size)
            {
              listOfString = {|ECS1200:new List<string>()|};
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

    [Fact]
    public async Task CodeFixInitializeInDeclarationWhenIdenticalNotDefaultInitializations()
    {
        const string testCode =
        """
          public class MyClass
          {
            private List<string> listOfString = new List<string>(5);

            public MyClass()
            {
              listOfString = {|ECS1200:new List<string>(5)|};
            }

            public MyClass(int size)
            {
              listOfString = {|ECS1200:new List<string>(5)|};
            }
          }
        """;

        const string fixedCode =
        """
          public class MyClass
          {
            private List<string> listOfString = new List<string>(5);

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

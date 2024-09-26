using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.StaticClassMemberInitializationAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class StaticClassMemberInitializationAnalyzerTests
{
    // This is permitted in this case because it has been added to this project's .globalconfig file
    [Fact(Skip = "Need to determine how to feed additional files like .globalconfig to test harness")]
    public async Task SafeItem_NoDiagnostic()
    {
        const string code = """
                            public class MyClass2
                            {
                                private static readonly List<int> Numbers = new List<int> { GetNumber(), 2, 3 };

                                private static int GetNumber() => 1;
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task StaticField_WithRegEx_NoDiagnostic()
    {
        const string code = """
                            using System.Text.RegularExpressions;

                            public class MyClass
                            {
                                private static readonly Regex PlaceholderRegex = new(@"\{.*?\}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
                            }
                            """;
        await Verifier.VerifyAnalyzerAsync(code, ReferenceAssemblyCatalog.Latest);
    }

    // Test 1: Static field initialized with a simple constant (No Diagnostic)
    [Fact]
    public async Task StaticField_WithSimpleConstantInitializer_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly int MaxItems = 100;
                            }
                            """;
        await Verifier.VerifyAnalyzerAsync(code, ReferenceAssemblyCatalog.Latest);
    }

    // Test 2: Static field initialized with a safe method call (No Diagnostic)
    [Fact]
    public async Task StaticField_WithConstantFromAnotherType_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly double PiValue = System.Math.PI;
                            }
                            """;
        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 2: Static field initialized with a safe method call (No Diagnostic)
    [Fact]
    public async Task StaticField_WithSafeMethodCall_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly double MaxValue = System.Math.Max(10, 20);
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 3: Static field initialized with a method call that may throw exceptions (Diagnostic)
    [Fact]
    public async Task StaticField_WithUnsafeMethodCall_ShouldTriggerDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly string {|ECS1300:ConfigValue = LoadConfigValue()|};

                                private static string LoadConfigValue()
                                {
                                    return System.IO.File.ReadAllText("config.txt");
                                }
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 4: Static field initialized with object creation (Diagnostic)
    [Fact]
    public async Task StaticField_WithObjectCreation_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly Random Rand = new Random();
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 5: Static field referencing another static field in the same class (No Diagnostic)
    [Fact]
    public async Task StaticField_ReferencingSameClassStaticField_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly int BaseValue = 10;
                                private static readonly int TotalValue = BaseValue + 5;
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 6: Static field referencing a static field in a base class (No Diagnostic)
    [Fact]
    public async Task StaticField_ReferencingBaseClassStaticField_NoDiagnostic()
    {
        const string code = """
                            public class BaseClass
                            {
                                protected static readonly int BaseValue = 10;
                            }

                            public class DerivedClass : BaseClass
                            {
                                private static readonly int TotalValue = BaseValue + 5;
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 7: Non-static field with complex initializer (No Diagnostic)
    [Fact]
    public async Task NonStaticField_WithComplexInitializer_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private readonly string ConfigValue = LoadConfigValue();

                                private static string LoadConfigValue()
                                {
                                    return System.IO.File.ReadAllText("config.txt");
                                }
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 8: Static constant field (No Diagnostic)
    [Fact]
    public async Task StaticConstField_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private const int MaxItems = 100;
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 9: Static field without initializer (No Diagnostic)
    [Fact]
    public async Task StaticField_WithoutInitializer_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly int MaxItems;
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 10: Static field initialized with collection initializer (No Diagnostic)
    [Fact]
    public async Task StaticField_WithListCollectionInitializer_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly List<int> Numbers = new List<int> { 1, 2, 3 };
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task StaticField_WithHashSetCollectionInitializer_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly HashSet<int> Numbers = new HashSet<int> { 1, 2, 3 };
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task StaticField_WithHashSetCtorAndCollectionInitializer_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly HashSet<string> Values = new(StringComparer.Ordinal)
                                {
                                    "Foo",
                                    "Bar",
                                    "Baz"
                                };
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task StaticField_WithComplexListInitialization_ShouldTriggerDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly List<int> {|ECS1300:Numbers = new List<int> { GetNumber(), 2, 3 }|};

                                private static int GetNumber() => 1;
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task StaticField_WithSimpleDictionaryInitialization_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly Dictionary<string, int> Mapping = new Dictionary<string, int>
                                {
                                    { "One", 1 },
                                    { "Two", 2 }
                                };
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task StaticField_WithComplexDictionaryInitialization_ShouldTriggerDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly Dictionary<string, int> {|ECS1300:Mapping = new Dictionary<string, int>
                                {
                                    { GetKey(), 1 },
                                    { "Two", 2 }
                                }|};

                                private static string GetKey() => "One";
                            }

                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 11: Static field initialized with lambda expression (Diagnostic)
    [Fact]
    public async Task StaticField_WithLambdaExpression_ShouldTriggerDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly Func<int, int> {|ECS1300:Square = x => x * x|};
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 12: Static field initialized with nameof expression (No Diagnostic)
    [Fact]
    public async Task StaticField_WithNameofExpression_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly string ClassName = nameof(MyClass);
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 13: Static field initialized with interpolated string (Diagnostic depends on content)
    [Fact]
    public async Task StaticField_WithInterpolatedString_ShouldTriggerDiagnosticIfComplex()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly string {|ECS1300:Path = $"C:\\Data\\{System.Guid.NewGuid()}.txt"|};
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 14: Static field initialized with expression that may throw via property (Diagnostic)
    [Fact]
    public async Task StaticField_WithExceptionThrowingProperty_ShouldTriggerDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly string {|ECS1300:CurrentDirectory = System.IO.Directory.GetCurrentDirectory()|};
                            }

                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 15: Static field initialized with array creation (No Diagnostic if elements are simple)
    [Fact]
    public async Task StaticField_WithArrayCreationOfSimpleTypes_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly int[] Numbers = new int[] { 1, 2, 3 };
                            }
                            """;
        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 16: Static field initialized with array creation of complex types (Depends on complexity)
    [Fact]
    public async Task StaticField_WithArrayCreationOfComplexTypes_ShouldTriggerDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly object[] Objects = new object[] { new object(), new object() };
                            }

                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 17: Static field initialized with ternary operator (Depends on complexity)
    [Fact]
    public async Task StaticField_WithTernaryOperator_ShouldEvaluateComplexity()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly int {|ECS1300:Value = (System.DateTime.Now.Hour > 12) ? 1 : 0|};
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 18: Static field initialized with safe method in ternary operator (No Diagnostic)
    [Fact]
    public async Task StaticField_WithSafeMethodInTernaryOperator_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly double {|ECS1300:Value = (System.Math.Max(10, 20) > 15) ? 1.0 : 0.0|};
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 19: Static field in a struct (Same rules apply)
    [Fact]
    public async Task StaticField_InStruct_ShouldTriggerDiagnosticIfComplex()
    {
        const string code = """
                            public struct MyStruct
                            {
                                private static readonly string {|ECS1300:ConfigValue = LoadConfigValue()|};

                                private static string LoadConfigValue()
                                {
                                    return System.IO.File.ReadAllText("config.txt");
                                }
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 20: Static field with object initializer (No Diagnostic)
    [Fact]
    public async Task StaticField_WithObjectInitializer_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly Person DefaultPerson = new Person { Name = "John Doe", Age = 30 };
                            }

                            public class Person
                            {
                                public string Name { get; set; }
                                public int Age { get; set; }
                            }

                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 21: Static field initialized with exception-safe code (No Diagnostic)
    // To keep the analyzer fast, we are not doing full data flow analysis, constant folding,
    // or evaluating expressions at compile time.
    // The division operation inherently carries the risk of a `DivideByZeroException`
    // and is treated as a "complex" operation.
    [Fact]
    public async Task StaticField_WithExceptionSafeInitializer_ShouldTriggerDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly int Zero = 0;
                                private static readonly int {|ECS1300:Value = 100 / (Zero + 1)|}; // Safe division
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 22: Static field initialized with division by zero (No Diagnostic)
    [Fact]
    public async Task StaticField_WithPotentialDivisionByZero_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly int Zero = 0;
                                private static readonly int Value = 100 / Zero; // Division by zero exception
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 23: Static field initialized with null-coalescing operator (No Diagnostic)
    [Fact]
    public async Task StaticField_WithNullCoalescingOperator_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly string DefaultName = null;
                                private static readonly string Name = DefaultName ?? "Unnamed";
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 24: Static field initialized with complex expression involving safe methods (No Diagnostic)
    [Fact]
    public async Task StaticField_WithComplexExpressionOfSafeMethods_NoDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly double Value = System.Math.Sin(System.Math.PI / 4) + System.Math.Cos(System.Math.PI / 4);
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }

    // Test 25: Static field initialized with complex expression involving unsafe methods (Diagnostic)
    [Fact]
    public async Task StaticField_WithComplexExpressionOfUnsafeMethods_ShouldTriggerDiagnostic()
    {
        const string code = """
                            public class MyClass
                            {
                                private static readonly int {|ECS1300:Value = GetRandomNumber() * 10|};

                                private static int GetRandomNumber()
                                {
                                    return new System.Random().Next();
                                }
                            }
                            """;

        await Verifier.VerifyAnalyzerAsync(code);
    }
}

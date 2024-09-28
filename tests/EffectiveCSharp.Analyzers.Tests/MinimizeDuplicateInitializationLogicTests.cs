using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.MinimizeDuplicateInitializationLogicAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class MinimizeDuplicateInitializationLogicTests
{
    [Fact]
    public async Task DetectsDuplicateInitialization()
    {
        const string testCode = """
                                public class MyClass
                                {
                                    private int _value;

                                    public {|ECS1400:MyClass|}()
                                    {
                                        _value = 10;
                                    }

                                    public {|ECS1400:MyClass|}(int value)
                                    {
                                        _value = 10;
                                    }
                                }
                                """;

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DetectsHelperMethod()
    {
        const string testCode = """
                                public class MyClass
                                {
                                    private List<string> data;
                                    private string name;

                                    public {|ECS1400:MyClass|}()
                                    {
                                        commonConstructor(0, string.Empty);
                                    }

                                    public {|ECS1400:MyClass|}(int initialCount)
                                    {
                                        commonConstructor(initialCount, string.Empty);
                                    }

                                    public {|ECS1400:MyClass|}(int initialCount, string name)
                                    {
                                        commonConstructor(initialCount, name);
                                    }

                                    private void commonConstructor(int count, string name)
                                    {
                                        this.data = (count > 0) ? new List<string>(count) : new List<string>();
                                        this.name = name;
                                    }
                                }
                                """;

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotDetectChainedConstructors()
    {
        const string testCode = """
                                public class MyClass
                                {
                                    private List<string> data;
                                    private string name;

                                    public MyClass() : this(0, string.Empty)
                                    {
                                    }

                                    public MyClass(int initialCount) : this(initialCount, string.Empty)
                                    {
                                    }

                                    public MyClass(int initialCount, string name)
                                    {
                                        this.data = (initialCount > 0) ? new List<string>(initialCount) : new List<string>();
                                        this.name = name;
                                    }
                                }
                                """;

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotDetectInDerivedClass()
    {
        const string testCode = """
                                public class BaseClass
                                {
                                    protected int _value;

                                    public BaseClass(int value)
                                    {
                                        _value = value;
                                    }
                                }

                                public class DerivedClass : BaseClass
                                {
                                    public DerivedClass(int value) : base(value)
                                    {
                                        _value = value;
                                    }

                                    public DerivedClass() : base(10)
                                    {
                                        _value = 10;
                                    }
                                }
                                """;

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DoesNotDetectInInstanceAndStaticCtor()
    {
        const string testCode = """
                                public class TestClass
                                {
                                    private static int _staticValue;

                                    static TestClass()
                                    {
                                        _staticValue = 10;
                                    }

                                    public TestClass()
                                    {
                                        _staticValue = 10;
                                    }
                                }
                                """;

        await Verifier.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DetectsComplexInitializationLogic()
    {
        const string testCode = """
                                public class TestClass
                                {
                                    private int _value;
                                    private int _otherValue;

                                    public {|ECS1400:TestClass|}()
                                    {
                                        _value = ComputeValue();
                                        _otherValue = 20;
                                    }

                                    public {|ECS1400:TestClass|}(int value)
                                    {
                                        _value = ComputeValue();
                                        _otherValue = 20;
                                    }

                                    private int ComputeValue()
                                    {
                                        return 42;
                                    }
                                }
                                """;

        await Verifier.VerifyAnalyzerAsync(testCode);
    }
}

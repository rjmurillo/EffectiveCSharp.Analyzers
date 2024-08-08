using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.MinimizeBoxingUnboxingAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

#pragma warning disable SA1204  // Static members are grouped with their Theory
#pragma warning disable SA1001  // The harness has literal code as a string, which can be weirdly formatted
#pragma warning disable SA1113
#pragma warning disable S125    // There's code in comments as examples
#pragma warning disable MA0051  // Some test methods are "too long"
#pragma warning disable MA0007  // There are multiple types of tests defined in theory data
#pragma warning disable IDE0028 // We cannot simply object creation on TheoryData because we need to convert from object[] to string, the way it is now is cleaner
#pragma warning disable AsyncFixer01

public class AvoidBoxingUnboxingTests
{
    public static TheoryData<string, string> TestData()
    {
        TheoryData<string> data = new()
            {
                // This should fire
                """
                int i = 5;
                object o = {|ECS0009:i|}; // boxing
                """,
                """
                int i = 5;
                object o = {|ECS0009:(object)i|}; // boxing
                """,

                // This should fire for method call with value type defined in System.Object
                """
                int i = 5;
                Console.WriteLine(i); // boxing
                """,

                // This should not fire because it's suppressed
                """
                #pragma warning disable ECS0009 // Minimize boxing and unboxing
                int i = 5;
                object o = i; // boxing
                #pragma warning restore ECS0009 // Minimize boxing and unboxing
                """,
                """
                var dt = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
                """,
            };

        return data.WithReferenceAssemblyGroups();
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

    public static TheoryData<string, string> TestData2()
    {
        TheoryData<string> data = new()
        {
            // This should fire
            // Equal to
            // Method("a few number...", new object[3]{ (object)firstNumber... });
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
            """,

            // This should not fire because it's suppressed
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
            """,

            // This should not fire because the string interpolation does not box
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
            """,
            """
            private const int MyNumber = 42;

            void Foo()
            {
              for(var i = 0; i <= MyNumber; i++)
              {
                Method("Bar");
              }
            }
            """,

            // Regression test: we are too aggressive when assigning ctor params to read-only properties
            """
            public int Arg { get; }

            public MyClass(int arg)
            {
                Arg = arg;
            }
            """,
        };

        return data.WithReferenceAssemblyGroups();
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

    [Fact]
    public async Task Return_Boxing_Detected()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class TestClass
            {
                public object ReturnBoxing()
                {
                    int i = 42;
                    return {|ECS0009:i|};
                }
            }
            """,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Argument_Boxing_Detected()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class TestClass
            {
                public void TakeObject(object obj) {}

                public void Method()
                {
                    int i = 42;
                    TakeObject({|ECS0009:i|});
                }
            }
            """,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task SimpleAssignment_Boxing_Detected()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class TestClass
            {
                public void AssignExample()
                {
                    int i = 42;
                    object boxed = {|ECS0009:i|};
                }
            }
            """,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task ArrayElementReference_Unboxing_Detected()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class TestClass
            {
                public void ArrayAccessExample()
                {
                    List<object> list = new List<object>();
                    int i = 42;
                    list.Add({|ECS0009:i|});    // Boxing operation
                    int value = {|ECS0009:(int)list[0]|}; // Unboxing operation
                }
            }
            """,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Assignment_With_Unboxing_Operation_Detected()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class TestClass
            {
                public void TestMethod()
                {
                    object obj = {|ECS0009:42|};        // Boxing operation expected here
                    int value = {|ECS0009:(int)obj|};  // Unboxing operation expected here
                }
            }
            """,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Assignment_With_Boxing_When_ValueType_As_Interface()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public interface ITestInterface
            {
                void TestMethod();
            }

            public struct TestStruct : ITestInterface
            {
                public int Value;
                public void TestMethod() { }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    TestStruct myStruct = new TestStruct { Value = 42 };
                    ITestInterface myInterface = {|ECS0009:myStruct|};  // Expected to trigger boxing warning
                }
            }
            """,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Implicit_boxing_in_List()
    {
        // We need to ensure the detection of boxing in cases where a value type
        // is being assigned or accessed in a way that results in copy semantics
        //
        // In this case, we are copying the value type when we bring it in and out
        // the reference type List<Person>.
        await Verifier.VerifyAnalyzerAsync(
            """
            internal class Program
            {
              static void Main()
              {

                // Using the Person in a collection
                var attendees = new List<Person>();
                var p = new Person { Name = "Old Name" };
                attendees.Add(p);

                // Try to change the name
                var p2 = {|ECS0009:attendees[0]|};
                p2.Name = "New Name";

                // Writes "Old Name" because we pulled a copy of the struct
                Console.WriteLine({|ECS0009:attendees[0]|}.ToString());
              }
            }

            public struct Person
            {
              public string Name { get; set; }
              public override string ToString() => Name;
            }
            """
            , ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Implicit_boxing_in_Dictionary_As_Value()
    {
        // We need to ensure the detection of boxing in cases where a value type
        // is being assigned or accessed in a way that results in copy semantics
        //
        // In this case, we are copying the value type when we bring it in and out
        // the reference type List<Person>.
        await Verifier.VerifyAnalyzerAsync(
            """
            internal class Program
            {
              static void Main()
              {

                // Using the Person in a collection
                var attendees = new Dictionary<int, Person>();
                var p = new Person { Name = "Old Name" };
                attendees.Add(1, p);

                // Try to change the name
                var p2 = {|ECS0009:attendees[1]|};
                p2.Name = "New Name";

                // Writes "Old Name" because we pulled a copy of the struct
                Console.WriteLine({|ECS0009:attendees[1]|}.ToString());
              }
            }

            public struct Person
            {
              public string Name { get; set; }
              public override string ToString() => Name;
            }
            """
            , ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Lowered_string_does_not_box()
    {
        // The code for name is getting flagged
        // It's lowered to
        //
        // string item = string.Concat("Foo", num.ToString());
        await Verifier.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;
            public class C {
                public void M() {
                    List<string> names = new List<String>();
                    for(var i = 0; i<100; i++) {
                        var name = "Foo" + i;
                        names.Add(name);
                    }
                }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task Foo()
    {
        // The code for name is getting flagged
        // It's lowered to
        //
        // string item = string.Concat("Foo", num.ToString());
        await Verifier.VerifyAnalyzerAsync(
            """
            public class C {
                public void M() {
                        var i = 0;
                        var name = "Foo" + i;
                }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }
}

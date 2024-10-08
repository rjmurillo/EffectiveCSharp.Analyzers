﻿using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.ExpressCallbacksWithDelegatesAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

public class ExpressCallbacksAsDelegateTests
{
    [Fact]
    public async Task Analyzer()
    {
        // The Find() method takes a delegate, in the form of
        // Predicate<int>, to perform a test on each element
        // of the list.
        //
        // TrueForAll() is similar
        // RemoveAll() modifies the list container when the predicate is true
        // ForEach() takes an Action<int> delegate
        //
        // The compiler converts the lambda expression into a method
        // and creates a delegate to that method
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              public void Test()
              {
                List<int> numbers = Enumerable.Range(1, 200).ToList();
                var oddNumbers = numbers.Find(n=> n % 2 == 1);
                var test = numbers.TrueForAll(n=> n < 50);

                numbers.RemoveAll(n => n % 2 == 0);

                numbers.ForEach(item => Console.WriteLine(item));
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task Delegate()
    {
        // The problem here is that this works as a single delegate
        // but when using it as a multicast delegate, things break.
        // The value returned from invoking the delegate is the
        // return value from the last function in the multicast chain.
        // The return from the CheckWithUser() predicate is ignored.
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MyClass
            {
              List<MyClass> container = new();

              public void LengthyOperation(Func<bool> predicate)
              {
                foreach(MyClass item in container)
                {
                    item.DoLengthyOperation();
                    if (predicate() == false)
                    {
                        return;
                    }
                }
              }

              public void DoLengthyOperation() {}
              public bool CheckWithUser() => true;
              public bool CheckWithSystem() => true;

              public void CheckThenDo()
              {
                Func<bool> cp = () => CheckWithUser();
                cp += () => CheckWithSystem();
                {|ECS0700:LengthyOperation(cp)|};
              }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task CorrectDelegate()
    {
        // You address both issues described in the Delegate test
        // by invoking the delegate target yourself. Each delegate
        // you create contains a list of delegates. You examine
        // the chain yourself and call each one.
        await Verifier.VerifyAnalyzerAsync(
            """
            using System.Linq;

            public class MyClass
            {
              List<MyClass> container = new();

              public void LengthyOperation(Func<bool> predicate)
              {
                bool canContinue = true;
                foreach(MyClass item in container)
                {
                  item.DoLengthyOperation();

                  foreach(Func<bool> pr in predicate.GetInvocationList())
                  {
                    canContinue &= pr();

                    if (!canContinue)
                    {
                      return;
                    }
                  }
                }
              }

              public void DoLengthyOperation() {}
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task CustomCollectionWithCallback()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class CustomCollection<T>
            {
                private List<T> _items = new List<T>();

                public void Add(T item)
                {
                    _items.Add(item);
                }

                public void ProcessItems(Action<T> callback)
                {
                    foreach (var item in _items)
                    {
                        callback(item);
                    }
                }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    var collection = new CustomCollection<int>();
                    collection.Add(1);
                    collection.Add(2);
                    collection.Add(3);

                    collection.ProcessItems(item => Console.WriteLine(item));
                }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task MultiDelegateCallbackHandling()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class MultiDelegateHandler
            {
                private List<int> _items = new List<int> { 1, 2, 3, 4, 5 };

                public void HandleItems(Action<int> actionCallback, Predicate<int> predicateCallback)
                {
                    foreach (var item in _items)
                    {
                        if (predicateCallback(item))
                        {
                            actionCallback(item);
                        }
                    }
                }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    var handler = new MultiDelegateHandler();
                    handler.HandleItems(
                        item => Console.WriteLine($"Processing item: {item}"),
                        item => item % 2 == 0
                    );
                }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task CallbackWithFunctionDelegate()
    {
        await Verifier.VerifyAnalyzerAsync(
            """
            public class TransformProcessor
            {
                private List<int> _items = new List<int> { 1, 2, 3, 4, 5 };

                public void ProcessItems(Func<int, string> transformCallback, Action<string> actionCallback)
                {
                    foreach (var item in _items)
                    {
                        var transformedItem = transformCallback(item);
                        actionCallback(transformedItem);
                    }
                }
            }

            public class TestClass
            {
                public void TestMethod()
                {
                    var processor = new TransformProcessor();
                    processor.ProcessItems(
                        item => $"Item: {item}",
                        transformedItem => Console.WriteLine(transformedItem)
                    );
                }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }

    [Fact]
    public async Task Private_Delegate()
    {
        // The analyzer SHOULD NOT trigger in this scenario.
        // The delegate is being used as intended, without being
        // passed around or combined in a manner that could cause
        // issues. The code is simple and straightforward, adhering
        // to safe and typical usage of delegates for callbacks.
        await Verifier.VerifyAnalyzerAsync(
            """
            public class C
            {
                private Func<char, bool> _predicate = null;

                public C()
                {
                    _predicate = char.IsLower;
                }

                public bool M(string s)
                {
                    return SpanExtensions.All(s.AsSpan(), _predicate);
                }

                public bool N(string s)
                {
                    return s.AsSpan().All(_predicate);
                }
            }

            public static class SpanExtensions
            {
                public static bool All(this ReadOnlySpan<char> source, Func<char, bool> predicate)
                {
                    for (var i = 0; i < source.Length; i++)
                    {
                        if (!predicate(source[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
            """,
            ReferenceAssemblyCatalog.Latest);
    }
}

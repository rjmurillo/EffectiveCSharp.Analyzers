# Effective C# item map

If you are reading *Effective C#: 50 Specific Ways to Improve Your C#, Third Edition* by Bill Wagner, you can check many of its items automatically with the [EffectiveCSharp.Analyzers](https://www.nuget.org/packages/EffectiveCSharp.Analyzers) NuGet package. It adds Roslyn analyzers, and code fixes for several of them, that flag code which does not follow the book's guidance.

Install it in your project:

```shell
dotnet add package EffectiveCSharp.Analyzers
```

Today 11 of the 50 items have a rule. Every rule ships with `Info` severity, and you can change the severity in your `.editorconfig`.

## How rule IDs map to items

Rule IDs follow the book's item numbers. Item N maps to `ECS` plus N padded to two digits plus `00`. For example, Item 2 maps to `ECS0200` and Item 13 maps to `ECS1300`. See [Documenting analyzers](DOCUMENTING-ANALYZERS.md) for the convention.

## Item map

Item numbers and topics follow the Third Edition table of contents. Rule titles come from the analyzer source.

| Item | Topic | Rule ID | Status |
|------|-------|---------|--------|
| 1 | Prefer Implicitly Typed Local Variables | [ECS0100](rules/ECS0100.md): Prefer implicitly typed local variables | implemented |
| 2 | Prefer readonly to const | [ECS0200](rules/ECS0200.md): Prefer readonly over const | implemented |
| 3 | Prefer the is or as Operators to Casts | None | not covered |
| 4 | Replace string.Format() with Interpolated Strings | [ECS0400](rules/ECS0400.md): Replace string.Format with interpolated string | implemented |
| 5 | Prefer FormattableString for Culture-Specific Strings | [ECS0500](rules/ECS0500.md): Prefer FormattableString or string.Create for culture-specific strings | implemented |
| 6 | Avoid String-ly Typed APIs | [ECS0600](rules/ECS0600.md): Avoid stringly-typed APIs | implemented |
| 7 | Express Callbacks with Delegates | [ECS0700](rules/ECS0700.md): Express callbacks with delegates | implemented |
| 8 | Use the Null Conditional Operator for Event Invocations | [ECS0800](rules/ECS0800.md): Use the Null Conditional Operator for Event Invocations | implemented |
| 9 | Minimize Boxing and Unboxing | [ECS0900](rules/ECS0900.md): Minimize boxing and unboxing | implemented |
| 10 | Use the new Modifier Only to React to Base Class Updates | None | not covered |
| 11 | Understand .NET Resource Management | None | not covered |
| 12 | Prefer Member Initializers to Assignment Statements | [ECS1200](rules/ECS1200.md): Prefer member initializers to assignment statements | implemented |
| 13 | Use Proper Initialization for Static Class Members | [ECS1300](rules/ECS1300.md): Use proper initialization for static class members | implemented |
| 14 | Minimize Duplicate Initialization Logic | [ECS1400](rules/ECS1400.md): Minimize duplicate initialization logic | implemented |
| 15 | Avoid Creating Unnecessary Objects | None | not covered |
| 16 | Never Call Virtual Functions in Constructors | None | not covered |
| 17 | Implement the Standard Dispose Pattern | None | not covered |
| 18 | Always Define Constraints That Are Minimal and Sufficient | None | not covered |
| 19 | Specialize Generic Algorithms Using Runtime Type Checking | None | not covered |
| 20 | Implement Ordering Relations with IComparable and IComparer | None | not covered |
| 21 | Always Create Generic Classes That Support Disposable Type Parameters | None | not covered |
| 22 | Support Generic Covariance and Contravariance | None | not covered |
| 23 | Use Delegates to Define Method Constraints on Type Parameters | None | not covered |
| 24 | Do Not Create Generic Specialization on Base Classes or Interfaces | None | not covered |
| 25 | Prefer Generic Methods Unless Type Parameters Are Instance Fields | None | not covered |
| 26 | Implement Classic Interfaces in Addition to Generic Interfaces | None | not covered |
| 27 | Augment Minimal Interface Contracts with Extension Methods | None | not covered |
| 28 | Consider Enhancing Constructed Types with Extension Methods | None | not covered |
| 29 | Prefer Iterator Methods to Returning Collections | None | not covered |
| 30 | Prefer Query Syntax to Loops | None | not covered |
| 31 | Create Composable APIs for Sequences | None | not covered |
| 32 | Decouple Iterations from Actions, Predicates, and Functions | None | not covered |
| 33 | Generate Sequence Items as Requested | None | not covered |
| 34 | Loosen Coupling by Using Function Parameters | None | not covered |
| 35 | Never Overload Extension Methods | None | not covered |
| 36 | Understand How Query Expressions Map to Method Calls | None | not covered |
| 37 | Prefer Lazy Evaluation to Eager Evaluation in Queries | None | not covered |
| 38 | Prefer Lambda Expressions to Methods | None | not covered |
| 39 | Avoid Throwing Exceptions in Functions and Actions | None | not covered |
| 40 | Distinguish Early from Deferred Execution | None | not covered |
| 41 | Avoid Capturing Expensive Resources | None | not covered |
| 42 | Distinguish between IEnumerable and IQueryable Data Sources | None | not covered |
| 43 | Use Single() and First() to Enforce Semantic Expectations on Queries | None | not covered |
| 44 | Avoid Modifying Bound Variables | None | not covered |
| 45 | Use Exceptions to Report Method Contract Failures | None | not covered |
| 46 | Utilize using and try/finally for Resource Cleanup | None | not covered |
| 47 | Create Complete Application-Specific Exception Classes | None | not covered |
| 48 | Prefer the Strong Exception Guarantee | None | not covered |
| 49 | Prefer Exception Filters to catch and re-throw | None | not covered |
| 50 | Leverage Side Effects in Exception Filters | None | not covered |

For release history, see [AnalyzerReleases.Shipped.md](../src/EffectiveCSharp.Analyzers/AnalyzerReleases.Shipped.md) and [AnalyzerReleases.Unshipped.md](../src/EffectiveCSharp.Analyzers/AnalyzerReleases.Unshipped.md).

## FAQ

### Is there an analyzer for Effective C#?

Yes. Install the `EffectiveCSharp.Analyzers` package from NuGet and the rules in the table above run as part of your build and in your IDE. Items marked "not covered" have no rule yet. If you want one, open an issue or see [CONTRIBUTING.md](../CONTRIBUTING.md).

### How does this relate to BillWagner/EffectiveCSharpAnalyzers?

[BillWagner/EffectiveCSharpAnalyzers](https://github.com/BillWagner/EffectiveCSharpAnalyzers) is a repository by the book's author with analyzers for the book's recommendations. Its last push was in November 2016 and the README of this project notes that it is not maintained. This project is a separate, independent implementation. It is not a fork of that repository and it is not affiliated with the author or the publisher.

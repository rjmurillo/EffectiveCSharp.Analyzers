# Effective CSharp Analyzers

Many of the recommendations in the book [Effective C#: 50 Specific Ways to Improve Your C#](https://www.oreilly.com/library/view/effective-c-50/9780134579290/) can be validated by Roslyn-based analyzers and code fixes. The author of the book [Bill Wagner](https://github.com/BillWagner) has a [repository](https://github.com/BillWagner/EffectiveCSharpAnalyzers) but they are not maintained.

## Rules

Code analyzers and code fixes will be named and numbered similarly to the book. Some of the author's guidance is opinionated, and unless otherwise indicated as prescriptive guidance, will show up as `Info` or `Suggestion`. In your code base you can change the effective severity of the rules to suit your needs.

Where possible, documentation will be provided as a derivative work of the book, including test cases. 

For a list of rules, see [shipped](src/EffectiveCSharp.Analyzers/AnalyzerReleases.Shipped.md) and [unshipped](src/EffectiveCSharp.Analyzers/AnalyzerReleases.Unshipped.md).
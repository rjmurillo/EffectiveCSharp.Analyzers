# Documenting analyzers

Full documentation is available in [Effective C#: 50 Specific Ways to Improve your C#](https://www.oreilly.com/library/view/effective-c-50/9780134579290/) by author [Bill Wagner](https://github.com/BillWagner). Documentation of each rule is a derative work inspired by the explanation, prose, and examples contained within the book.

Code analyzers and code fixes will be named and numbered similarly to the book (e.g., _Item 2: Prefer readonly to const_ is tracked as [`ECS0002`](./rules/ECS0002.md)). Some of the author's guidance is opinionated, and unless otherwise indicated as prescriptive guidance, will show up as `Info` or `Suggestion`. In your code base you can change the effective severity of the rules to suit your needs.

For each analyzer, it will be documented as follows:

1. Within the `docs` at the root of the analyzer project

2. A subdirectory `docs/rules`

The rationale for this suggestion is that there might be other documents in the `docs` directory (like this one). Keeping the rule documentation in its own subdirectory makes it easier to distinguish from other documentation.

3. A copy of the [Rule reference page template](RULE-REFERENCE-TEMPLATE.md) is used to create documentation with the following convention:

    `<DiagnosticId>.md`

    For example, for _Item 2: Prefer readonly to const_ the prefix for Effective CSharp Analyzers is `ECS` and the identifier for the rule is normalized to four places. The documentation would be named `ECS0002.md`

4. The template is filled in with information about the analyzer.

5. Provide a stable URI for each page

If you use a URI that points to the GitHub repo `main` branch, then the URI will change whenever the source tree is rearranged. If rule behavior changes over time, it will be out of phase with the documentation used to describe the rule in the version used to identify the issue.

To avoid this issue, we use [`Nerdbank.GitVersioning`](https://github.com/dotnet/Nerdbank.GitVersioning) to stamp the commit into the assembly. 

6. In each analyzer, set the value of the `HelpLinkUri` property of the `DiagnosticDescriptor` to the URI.

For example, the [PreferReadonlyOverConstAnalyzer](../src/EffectiveCSharp.Analyzers/PreferReadonlyOverConstAnalyzer.cs) produces a diagnostic with rule ID `ECS0002` ("Prefer readonly over const").

```csharp
internal const string Id = DiagnosticIds.PreferReadonlyOverConst;

private static readonly DiagnosticDescriptor Rule = new(
    id: Id,
    title: "Prefer readonly over const",
    messageFormat: "Consider using readonly instead of const for better flexibility",
    category: "Maintainability",
    defaultSeverity: DiagnosticSeverity.Info,
    isEnabledByDefault: true,
    helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{Id}.md");
```

The documentation for the rule is placed in to `docs/rules/ECS0002.md`.

**Note**: Some analyzers produce diagnostics with more than one rule. In such a case, create a separate reference page for each rule following the conventions above.
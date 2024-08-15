# RULEID: Friendly rule name

This rule is described in detail in [Effective C#: 50 Specific Ways to Improve your C#](https://www.oreilly.com/library/view/effective-c-50/9780134579290/).

## Cause

## Rule description

## How to fix violations

## When to suppress warnings

### Suppress a warning

If you just want to suppress a single violation, add preprocessor directives to your source file to disable and then re-enable the rule.

```csharp
#pragma warning disable RULEID
// The code that's violating the rule
#pragma warning restore RULEID
```

To disable the rule for a file, folder, or project, set its severity to none in the [configuration file](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files).

```ini
[*.cs]
dotnet_diagnostic.RULEID.severity = none
```

## Example of a violation

### Description

### Code

```csharp
```

## Example of how to fix

### Description

### Code

```csharp
```

## Related rules

[RULEID: Friendly related rule name](https://stable-uris/MyRuleId.md)
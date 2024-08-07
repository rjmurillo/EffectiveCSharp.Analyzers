namespace EffectiveCSharp.Analyzers.Common;

internal static class DiagnosticExtensions
{
    [DebuggerStepThrough]
    internal static Diagnostic CreateDiagnostic(
        this SyntaxNode node,
        DiagnosticDescriptor rule,
        params object?[]? messageArgs)
        => node.CreateDiagnostic(rule, properties: null, messageArgs);

    [DebuggerStepThrough]
    internal static Diagnostic CreateDiagnostic(
        this SyntaxNode node,
        DiagnosticDescriptor rule,
        ImmutableDictionary<string, string?>? properties,
        params object?[]? messageArgs)
        => node.CreateDiagnostic(
            rule,
            additionalLocations: null,
            properties,
            messageArgs);

    [DebuggerStepThrough]
    internal static Diagnostic CreateDiagnostic(
        this SyntaxNode node,
        DiagnosticDescriptor rule,
        IEnumerable<Location>? additionalLocations,
        ImmutableDictionary<string, string?>? properties,
        params object?[]? messageArgs)
        => node
            .GetLocation()
            .CreateDiagnostic(
                rule: rule,
                additionalLocations: additionalLocations,
                properties: properties,
                messageArgs: messageArgs);

    [DebuggerStepThrough]
    internal static Diagnostic CreateDiagnostic(
        this Location location,
        DiagnosticDescriptor rule,
        params object?[]? messageArgs)
        => location
            .CreateDiagnostic(
                rule: rule,
                properties: null,
                messageArgs: messageArgs);

    internal static Diagnostic CreateDiagnostic(
        this Location location,
        DiagnosticDescriptor rule,
        ImmutableDictionary<string, string?>? properties,
        params object?[]? messageArgs)
        => location.CreateDiagnostic(
            rule: rule,
            additionalLocations: null,
            properties: properties,
            messageArgs: messageArgs);

    [DebuggerStepThrough]
    internal static Diagnostic CreateDiagnostic(
        this Location location,
        DiagnosticDescriptor rule,
        IEnumerable<Location>? additionalLocations,
        ImmutableDictionary<string, string?>? properties,
        params object?[]? messageArgs)
    {
        if (!location.IsInSource)
        {
            location = Location.None;
        }

        return Diagnostic.Create(
            descriptor: rule,
            location: location,
            additionalLocations: additionalLocations,
            properties: properties,
            messageArgs: messageArgs);
    }
}

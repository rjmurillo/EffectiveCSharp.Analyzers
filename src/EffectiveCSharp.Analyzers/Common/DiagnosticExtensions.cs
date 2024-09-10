using System.Runtime.CompilerServices;

namespace EffectiveCSharp.Analyzers.Common;

internal static class DiagnosticExtensions
{
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Diagnostic CreateDiagnostic(
        this Location location,
        DiagnosticDescriptor rule,
        params object?[]? messageArgs)
        => location
            .CreateDiagnostic(
                rule: rule,
                properties: null,
                messageArgs: messageArgs);

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

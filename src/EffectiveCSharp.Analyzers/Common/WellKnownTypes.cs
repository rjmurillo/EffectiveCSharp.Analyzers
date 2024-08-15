namespace EffectiveCSharp.Analyzers.Common;

#pragma warning disable ECS0200 // Consider using static readonly instead of const

internal static class WellKnownTypes
{
    internal const string Span = nameof(Span);
    internal const string ReadOnlySpan = nameof(ReadOnlySpan);
}

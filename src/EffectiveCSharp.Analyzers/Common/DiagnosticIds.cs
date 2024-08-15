namespace EffectiveCSharp.Analyzers;

// Diagnostic IDs must be a non-null constant
#pragma warning disable ECS0200 // Consider using readonly instead of const for better flexibility

internal static class DiagnosticIds
{
    internal const string PreferImplicitlyTypedLocalVariables = "ECS0100";
    internal const string PreferReadonlyOverConst = "ECS0200";
    internal const string ReplaceStringFormatWithInterpolatedString = "ECS0400";
    internal const string PreferFormattableStringForCultureSpecificStrings = "ECS0500";
    internal const string AvoidStringlyTypedApis = "ECS0600";
    internal const string ExpressCallbacksWithDelegates = "ECS0700";
    internal const string UseNullConditionalOperatorForEventInvocations = "ECS0800";
    internal const string MinimizeBoxingUnboxing = "ECS0900";
    internal const string BeAwareOfValueTypeCopyInReferenceTypes = "ECS0901";
    internal const string UseSpanInstead = "ECS1000";
}

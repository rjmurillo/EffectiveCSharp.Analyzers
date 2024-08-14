namespace EffectiveCSharp.Analyzers;

// Diagnostic IDs must be a non-null constant
#pragma warning disable ECS0002 // Consider using readonly instead of const for better flexibility

internal static class DiagnosticIds
{
    internal const string PreferImplicitlyTypedLocalVariables = "ECS0001";
    internal const string PreferReadonlyOverConst = "ECS0002";
    internal const string ReplaceStringFormatWithInterpolatedString = "ECS0004";
    internal const string PreferFormattableStringForCultureSpecificStrings = "ECS0005";
    internal const string AvoidStringlyTypedApis = "ECS0006";
    internal const string ExpressCallbacksWithDelegates = "ECS0007";
    internal const string UseNullConditionalOperatorForEventInvocations = "ECS0008";
    internal const string MinimizeBoxingUnboxing = "ECS0009";
    internal const string BeAwareOfValueTypeCopyInReferenceTypes = "ECS0009";
    internal const string UseSpanInstead = "ECS1000";
}

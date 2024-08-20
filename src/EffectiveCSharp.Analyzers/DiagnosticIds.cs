﻿namespace EffectiveCSharp.Analyzers;

internal static class DiagnosticIds
{
    internal const string PreferImplicitlyTypedLocalVariables = "ECS0001";
    internal const string PreferReadonlyOverConst = "ECS0002";
    internal const string AvoidStringlyTypedApis = "ECS0006";
    internal const string MinimizeBoxingUnboxing = "ECS0009";
    internal const string BeAwareOfValueTypeCopyInReferenceTypes = "ECS0009";
    internal const string UseSpanInstead = "ECS1000";
    internal const string PreferDeclarationInitializersToAssignmentStatement = "ECS1200";
    internal const string PreferDeclarationInitializersExceptNullOrZero = "ECS1201";
    internal const string PreferDeclarationInitializersExceptWhenVaryingInitializations = "ECS1202";
    internal const string PreferDeclarationInitializersWhenNoInitializationPresent = "ECS1203";
}

﻿namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #9 - Minimize boxing and unboxing.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MinimizeBoxingUnboxingAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.MinimizeBoxingUnboxing;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Minimize boxing and unboxing",
        messageFormat: "Consider using an alternative implementation to avoid boxing and unboxing",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Conversion, OperationKind.SimpleAssignment, OperationKind.Argument, OperationKind.Return, OperationKind.PropertyReference, OperationKind.FieldReference, OperationKind.Invocation, OperationKind.ArrayElementReference, OperationKind.ObjectCreation, OperationKind.ArrayElementReference);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        switch (context.Operation)
        {
            case IConversionOperation conversionOperation:
                AnalyzeConversionOperation(conversionOperation, context);
                break;

            case ISimpleAssignmentOperation simpleAssignmentOperation:
                AnalyzeSimpleAssignmentOperation(simpleAssignmentOperation, context);
                break;

            case IVariableDeclarationOperation variableDeclarationOperation:
                AnalyzeVariableDeclarationOperation(variableDeclarationOperation, context);
                break;

            case IArgumentOperation argumentOperation:
                AnalyzeArgumentOperation(argumentOperation, context);
                break;

            case IReturnOperation returnOperation:
                AnalyzeReturnOperation(returnOperation, context);
                break;

            case IPropertyReferenceOperation propertyReferenceOperation:
                AnalyzePropertyReferenceOperation(propertyReferenceOperation, context);
                break;

            case IFieldReferenceOperation fieldReferenceOperation:
                AnalyzeFieldReferenceOperation(fieldReferenceOperation, context);
                break;

            case IMemberReferenceOperation memberReferenceOperation:
                AnalyzeMemberReferenceOperation(memberReferenceOperation, context);
                break;

            case IObjectCreationOperation objectCreationOperation:
                AnalyzeObjectCreationOperation(objectCreationOperation, context);
                break;

            case IInvocationOperation invocationOperation:
                AnalyzeInvocationOperation(invocationOperation, context);
                break;

            case IArrayElementReferenceOperation arrayElementReferenceOperation:
                AnalyzeArrayElementReferenceOperation(arrayElementReferenceOperation, context);
                break;
        }
    }

    private static void AnalyzeArrayElementReferenceOperation(IArrayElementReferenceOperation arrayElementReferenceOperation, OperationAnalysisContext context)
    {
        if (arrayElementReferenceOperation.Type?.IsValueType == true
            && context.ContainingSymbol?.ContainingType?.IsReferenceType == true)
        {
            Diagnostic diagnostic = arrayElementReferenceOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeInvocationOperation(IInvocationOperation invocationOperation, OperationAnalysisContext context)
    {
        if (invocationOperation.TargetMethod.ReturnType?.IsValueType == true
            && invocationOperation.TargetMethod.ReturnType.IsReferenceType
            && !IsNameOf(invocationOperation))
        {
            Diagnostic diagnostic = invocationOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeObjectCreationOperation(IObjectCreationOperation objectCreationOperation, OperationAnalysisContext context)
    {
        // Skip any type creation that doesn't involve value types being boxed to object
        if (objectCreationOperation.Type is { IsValueType: true }
            && objectCreationOperation.Arguments.Any(arg => (arg.Value).IsBoxingOperation()))
        {
            Diagnostic diagnostic = objectCreationOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeConversionOperation(IConversionOperation conversionOperation, OperationAnalysisContext context)
    {
        if (conversionOperation.IsBoxingOperation())
        {
            Diagnostic diagnostic = conversionOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeSimpleAssignmentOperation(ISimpleAssignmentOperation simpleAssignmentOperation, OperationAnalysisContext context)
    {
        ITypeSymbol? targetType = simpleAssignmentOperation.Target.Type;
        ITypeSymbol? valueType = simpleAssignmentOperation.Value.Type;

        if (targetType?.IsReferenceType == true
            && valueType?.IsValueType == true
            && !simpleAssignmentOperation.Value.IsConstant()
            && !IsParameterToPropertyAssignment(simpleAssignmentOperation))
        {
            Diagnostic diagnostic = simpleAssignmentOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeVariableDeclarationOperation(IVariableDeclarationOperation variableDeclarationOperation, OperationAnalysisContext context)
    {
        foreach (IVariableDeclaratorOperation? declarator in variableDeclarationOperation.Declarators)
        {
            if (declarator.Initializer?.Value is IConversionOperation conversionOperation)
            {
                AnalyzeConversionOperation(conversionOperation, context);
            }
        }
    }

    private static void AnalyzeArgumentOperation(IArgumentOperation argumentOperation, OperationAnalysisContext context)
    {
        if (argumentOperation.Value.Type?.IsValueType == true
            && argumentOperation.Parameter?.Type.IsReferenceType == true)
        {
            Diagnostic diagnostic = argumentOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeReturnOperation(IReturnOperation returnOperation, OperationAnalysisContext context)
    {
        if (context.ContainingSymbol is IMethodSymbol methodSymbol
            && returnOperation.ReturnedValue.Type?.IsValueType == true
            && methodSymbol.ReturnType.IsReferenceType
            && !returnOperation.ReturnedValue.IsConstant())
        {
            Diagnostic diagnostic = returnOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeMemberReferenceOperation(IMemberReferenceOperation memberReferenceOperation, OperationAnalysisContext context)
    {
        if (memberReferenceOperation.Member.ContainingType.IsValueType)
        {
            IOperation? parentOperation = memberReferenceOperation.Parent;

            if (parentOperation is IPropertyReferenceOperation { Instance: IConversionOperation conversion }
                && conversion.Type.IsReferenceType)
            {
                Diagnostic diagnostic = memberReferenceOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void AnalyzePropertyReferenceOperation(IPropertyReferenceOperation propertyReferenceOperation, OperationAnalysisContext context)
    {
        if (propertyReferenceOperation.Parent.IsBoxingOperation())
        {
            Diagnostic diagnostic = propertyReferenceOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeFieldReferenceOperation(IFieldReferenceOperation fieldReferenceOperation, OperationAnalysisContext context)
    {
        if (fieldReferenceOperation.Parent.IsBoxingOperation()
            && !fieldReferenceOperation.IsConstant())
        {
            Diagnostic diagnostic = fieldReferenceOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsNameOf(IInvocationOperation invocationOperation)
    {
        return invocationOperation.Syntax is InvocationExpressionSyntax { Expression: IdentifierNameSyntax
        {
            Identifier.Text: "nameof"
        }
        };
    }

    private static bool IsParameterToPropertyAssignment(ISimpleAssignmentOperation simpleAssignmentOperation)
    {
        return simpleAssignmentOperation.Value.Kind == OperationKind.ParameterReference
               && simpleAssignmentOperation.Target.Kind == OperationKind.PropertyReference;
    }
}

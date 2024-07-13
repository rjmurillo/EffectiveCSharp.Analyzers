namespace EffectiveCSharp.Analyzers;

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
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Conversion, OperationKind.SimpleAssignment, OperationKind.Argument, OperationKind.Return, OperationKind.PropertyReference, OperationKind.FieldReference, OperationKind.Invocation, OperationKind.ArrayElementReference);
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

            case IInvocationOperation invocationOperation:
                AnalyzeInvocationOperation(invocationOperation, context);
                break;
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

    private static void AnalyzeConversionOperation(IConversionOperation conversionOperation, OperationAnalysisContext context)
    {
        if (IsBoxingOperation(conversionOperation))
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
            && !IsConstant(simpleAssignmentOperation.Value))
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
            && argumentOperation.Parameter.Type?.IsReferenceType == true)
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
            && !IsConstant(returnOperation.ReturnedValue))
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
        if (propertyReferenceOperation.Type?.IsValueType == true
            && context.ContainingSymbol?.ContainingType?.IsReferenceType == true
            && !IsConstant(propertyReferenceOperation))
        {
            Diagnostic diagnostic = propertyReferenceOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeFieldReferenceOperation(IFieldReferenceOperation fieldReferenceOperation, OperationAnalysisContext context)
    {
        if (fieldReferenceOperation.Type?.IsValueType == true
            && context.ContainingSymbol?.ContainingType?.IsReferenceType == true
            && !IsConstant(fieldReferenceOperation))
        {
            Diagnostic diagnostic = fieldReferenceOperation.Syntax.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsBoxingOperation(IConversionOperation conversionOperation)
    {
        return conversionOperation.Operand.Type?.IsValueType == true
               && conversionOperation.Type?.IsReferenceType == true;
    }

    private static bool IsConstant(IOperation operation)
    {
        return operation.ConstantValue.HasValue;
    }

    private static bool IsNameOf(IInvocationOperation invocationOperation)
    {
        return invocationOperation.Syntax is InvocationExpressionSyntax invocationSyntax &&
               invocationSyntax.Expression is IdentifierNameSyntax identifierNameSyntax &&
               identifierNameSyntax.Identifier.Text == "nameof";
    }
}

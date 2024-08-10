namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #5 - Use FormattableString for culture specific strings.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FormattableStringForCultureSpecificStringsAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = "Prefer FormattableString for culture-specific strings";
    private static readonly LocalizableString MessageFormat = "Use 'FormattableString' instead of 'string' for culture-specific interpolated strings";
    private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticIds.PreferFormattableStringForCultureSpecificStrings,
            Title,
            MessageFormat,
            Categories.Globalization,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticIds.PreferFormattableStringForCultureSpecificStrings}.md");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            CSharpParseOptions? parseOptions = compilationContext.Compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
            if (parseOptions != null && parseOptions.LanguageVersion < LanguageVersion.CSharp10)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InterpolatedStringExpression);
        });
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        InterpolatedStringExpressionSyntax interpolatedString = (InterpolatedStringExpressionSyntax)context.Node;
        SyntaxNode? parent = interpolatedString.Parent;

        if (parent == null || IsSimpleStringConcatenation(interpolatedString, context))
        {
            return;
        }

        ITypeSymbol? targetType = parent switch
        {
            AssignmentExpressionSyntax assignment => GetAssignmentTargetType(context, assignment),
            EqualsValueClauseSyntax equalsValueClause => GetEqualsValueClauseTargetType(context, equalsValueClause),
            ConditionalExpressionSyntax conditionalExpression => GetConditionalExpressionTargetType(context, conditionalExpression),
            ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax => GetLambdaTargetType(context, parent),
            ArgumentSyntax argument => GetArgumentTargetType(context, argument),
            _ => null,
        };

        if (targetType?.SpecialType == SpecialType.System_String)
        {
            ReportDiagnostic(context, interpolatedString, "FormattableString", "string");
        }
    }

    private static bool IsSimpleStringConcatenation(InterpolatedStringExpressionSyntax interpolatedString, SyntaxNodeAnalysisContext context)
    {
        foreach (InterpolatedStringContentSyntax content in interpolatedString.Contents)
        {
            if (content is not InterpolationSyntax interpolation)
            {
                continue;
            }

            TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(interpolation.Expression, context.CancellationToken);
            if (typeInfo.Type?.SpecialType != SpecialType.System_String
                || ContainsComplexFormatting(interpolation.Expression))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsComplexFormatting(ExpressionSyntax expression)
    {
        // Check if the expression contains any method invocations or more complex operations
        return expression is InvocationExpressionSyntax or BinaryExpressionSyntax;
    }

    private static ITypeSymbol? GetAssignmentTargetType(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment)
    {
        return context.SemanticModel.GetTypeInfo(assignment.Left, context.CancellationToken).Type;
    }

    private static ITypeSymbol? GetEqualsValueClauseTargetType(SyntaxNodeAnalysisContext context, EqualsValueClauseSyntax equalsValueClause)
    {
        SyntaxNode? declaration = equalsValueClause.Parent;
        switch (declaration)
        {
            case VariableDeclaratorSyntax variableDeclarator:
                ISymbol? symbol = context.SemanticModel.GetDeclaredSymbol(variableDeclarator, context.CancellationToken);
                ITypeSymbol? typeSymbol = (symbol as ILocalSymbol)?.Type ?? (symbol as IFieldSymbol)?.Type;

                // Check if the type is a generic (like Func<string>) and unpack the target type
                if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol)
                {
                    ITypeSymbol? returnType = namedTypeSymbol.TypeArguments.FirstOrDefault();
                    if (returnType?.SpecialType == SpecialType.System_String)
                    {
                        return returnType;
                    }
                }

                return typeSymbol;
            case PropertyDeclarationSyntax propertyDeclaration:
                return context.SemanticModel.GetTypeInfo(propertyDeclaration.Type, context.CancellationToken).Type;
            default:
                return null;
        }
    }

    private static ITypeSymbol? GetConditionalExpressionTargetType(SyntaxNodeAnalysisContext context, ConditionalExpressionSyntax conditionalExpression)
    {
        return context.SemanticModel.GetTypeInfo(conditionalExpression, context.CancellationToken).Type;
    }

    private static ITypeSymbol? GetLambdaTargetType(SyntaxNodeAnalysisContext context, SyntaxNode parent)
    {
        // Lambdas are usually within EqualsValueClauseSyntax, so we can reuse the method
        if (parent.Parent is EqualsValueClauseSyntax lambdaParent)
        {
            return GetEqualsValueClauseTargetType(context, lambdaParent);
        }

        return null;
    }

    private static ITypeSymbol? GetArgumentTargetType(SyntaxNodeAnalysisContext context, ArgumentSyntax argument)
    {
        if (argument.Parent?.Parent is InvocationExpressionSyntax methodInvocation
            && context.SemanticModel.GetSymbolInfo(methodInvocation, context.CancellationToken).Symbol is IMethodSymbol methodSymbol
            && string.Equals(methodSymbol.ContainingType.Name, "StringBuilder", StringComparison.Ordinal)
            && string.Equals(methodSymbol.ContainingNamespace.ToDisplayString(), "System.Text", StringComparison.Ordinal))
        {
            return context.Compilation.GetTypeByMetadataName("System.String");
        }

        return null;
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, InterpolatedStringExpressionSyntax interpolatedString, string suggestion, string original)
    {
        Diagnostic diagnostic = interpolatedString.GetLocation().CreateDiagnostic(Rule, suggestion, original);
        context.ReportDiagnostic(diagnostic);
    }
}

namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #5 - Use FormattableString for culture specific strings.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FormattableStringForCultureSpecificStringsAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = "Prefer FormattableString or string.Create for culture-specific strings";
    private static readonly LocalizableString MessageFormat = "Use '{0}' instead of '{1}' for culture-specific interpolated strings";
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
            // String interpolation was introduced in C# 6
            if (!compilationContext.Compilation.IsCSharpVersionOrLater(LanguageVersion.CSharp6))
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
            (Version? DotNetVersion, LanguageVersion? CompilerLanguageVersion) versions = context.Compilation.GetVersions();
            if (versions.CompilerLanguageVersion is null || versions.DotNetVersion is null)
            {
                return;
            }

            /*
             * To align the analyzer with the guidance provided by Stephen Toub for .NET 6 and later,
             * we should favor `string.Create` over `FormattableString` when formatting culture-specific
             * strings.
             *
             * See https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/
             */
            switch (versions.CompilerLanguageVersion)
            {
                // string.Create was introduced in C# 10 and .NET 6
                case >= LanguageVersion.CSharp10 when versions.DotNetVersion >= DotNet.Versions.DotNet6:
                    // Favor `string.Create`
                    ReportDiagnostic(context, interpolatedString, "string.Create", "string");
                    break;

                // Pre-.NET 6, favor FormattableString
                case >= LanguageVersion.CSharp9 when versions.DotNetVersion >= DotNet.Versions.DotNet5:
                    ReportDiagnostic(context, interpolatedString, "FormattableString", "string");
                    break;

                // Interpolated strings were introduced in C# 6 and .NET Framework 4.6, but we don't have fancy features
                case >= LanguageVersion.CSharp6 when versions.DotNetVersion >= DotNet.Versions.DotNet46:
                    ReportDiagnostic(context, interpolatedString, "string.Format", "string");
                    break;
            }
        }
    }



    private static bool IsSimpleStringConcatenation(InterpolatedStringExpressionSyntax interpolatedString, SyntaxNodeAnalysisContext context)
    {
        for (int i = 0; i < interpolatedString.Contents.Count; i++)
        {
            InterpolatedStringContentSyntax content = interpolatedString.Contents[i];
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

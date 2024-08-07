﻿namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #6 - Avoid stringly typed APIs.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AvoidStringlyTypedApisAnalyzer : DiagnosticAnalyzer
{
    private static readonly string Title = "Avoid stringly-typed APIs";
    private static readonly string MessageFormat = "Use 'nameof({0})' instead of the string literal \"{0}\"";
    private static readonly string Description = "Replace string literals representing member names with the nameof operator to ensure type safety.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.AvoidStringlyTypedApis,
        Title,
        MessageFormat,
        Categories.Refactoring,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticIds.AvoidStringlyTypedApis}.md");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeLiteralExpression, SyntaxKind.StringLiteralExpression);
    }

    private static void AnalyzeLiteralExpression(SyntaxNodeAnalysisContext context)
    {
        LiteralExpressionSyntax literalExpression = (LiteralExpressionSyntax)context.Node;
        string literalValue = literalExpression.Token.ValueText;

        // Walk up the syntax tree to find the containing class or method
        TypeDeclarationSyntax? containingClass = literalExpression.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        MethodDeclarationSyntax? containingMethod = literalExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();

        SemanticModel semanticModel = context.SemanticModel;

        if (containingClass != null
            && semanticModel.GetDeclaredSymbol(containingClass, context.CancellationToken) is { } containingTypeSymbol)
        {
            IEnumerable<string> memberNames = containingTypeSymbol.GetMembers().Select(member => member.Name);

            if (memberNames.Contains(literalValue, StringComparer.Ordinal))
            {
                Diagnostic diagnostic = literalExpression.GetLocation().CreateDiagnostic(Rule, literalValue);
                context.ReportDiagnostic(diagnostic);
            }
        }

        if (containingMethod != null
            && semanticModel.GetDeclaredSymbol(containingMethod, context.CancellationToken) is { } methodSymbol)
        {
            IEnumerable<string> parameterNames = methodSymbol.Parameters.Select(parameter => parameter.Name);

            if (parameterNames.Contains(literalValue, StringComparer.Ordinal))
            {
                Diagnostic diagnostic = literalExpression.GetLocation().CreateDiagnostic(Rule, literalValue);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

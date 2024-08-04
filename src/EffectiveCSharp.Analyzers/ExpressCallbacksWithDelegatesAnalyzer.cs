using System;
using System.Collections.Generic;
using System.Text;

namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #7 - Express callbacks with delegates.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ExpressCallbacksWithDelegatesAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.ExpressCallbacksWithDelegates;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Express callbacks with delegates",
        messageFormat: "Method '{0}' should use a delegate for the callback",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Ensure that callbacks are implemented using delegates.",
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        InvocationExpressionSyntax invocationExpr = (InvocationExpressionSyntax)context.Node;
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocationExpr, context.CancellationToken);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        // Check if the method has delegate parameters
        bool hasDelegateParameter = methodSymbol.Parameters.Any(p => IsDelegateType(p.Type));
        if (!hasDelegateParameter)
        {
            return;
        }

        // Check if the delegate is passed around or multicast
        if (ShouldWarnForMethod(invocationExpr, context.SemanticModel))
        {
            Diagnostic diagnostic = invocationExpr.GetLocation().CreateDiagnostic(Rule, methodSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool ShouldWarnForMethod(InvocationExpressionSyntax invocationExpr, SemanticModel semanticModel)
    {
#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions
        foreach (ArgumentSyntax argument in invocationExpr.ArgumentList.Arguments)
        {
            ITypeSymbol? argumentType = semanticModel.GetTypeInfo(argument.Expression).Type;

            // Check if the argument is a delegate type
            if (IsDelegateType(argumentType))
            {
                // Check if the delegate is passed to another method or assigned to a field/property
                SyntaxNode? parent = argument.Expression.Parent;
                while (parent != null)
                {
                    if (parent is ArgumentSyntax or AssignmentExpressionSyntax)
                    {
                        return true;
                    }

                    parent = parent.Parent;
                }

                // Additional checks for specific scenarios can be added here
            }
        }
#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions

        // No issues detected
        return false;
    }

    private static bool IsDelegateType(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol == null)
        {
            return false;
        }

        if (typeSymbol.TypeKind == TypeKind.Delegate)
        {
            return true;
        }

        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            return false;
        }

// It's true that this CAN be simplified, but the readability is better this way
#pragma warning disable IDE0046 // 'if' statement can be simplified
        // Handle Func<T>, Action<T>, Predicate<T>
        if (namedTypeSymbol.ConstructedFrom.Name.StartsWith("Func", StringComparison.Ordinal) ||
            namedTypeSymbol.ConstructedFrom.Name.StartsWith("Action", StringComparison.Ordinal) ||
            namedTypeSymbol.ConstructedFrom.Name.StartsWith("Predicate", StringComparison.Ordinal))
        {
            return true;
        }
#pragma warning restore IDE0046 // 'if' statement can be simplified

        return namedTypeSymbol.ConstructedFrom is { SpecialType: SpecialType.System_MulticastDelegate };
    }
}

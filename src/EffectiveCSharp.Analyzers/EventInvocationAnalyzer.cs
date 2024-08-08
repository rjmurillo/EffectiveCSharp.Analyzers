namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #8 - Use the Null Conditional Operator for Event Invocations.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EventInvocationAnalyzer : DiagnosticAnalyzer
{
    private static readonly string Title = "Use the Null Conditional Operator for Event Invocations";
    private static readonly string MessageFormat = "Use the null-conditional operator to invoke the event '{0}'";
    private static readonly string Description = "Event invocation should use the null-conditional operator to avoid race conditions and improve readability.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.UseNullConditionalOperatorForEventInvocations,
        Title,
        MessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticIds.UseNullConditionalOperatorForEventInvocations}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.IfStatement, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        switch (context.Node)
        {
            case IfStatementSyntax ifStatement:
                AnalyzeIfStatement(context, ifStatement);
                break;
            case InvocationExpressionSyntax invocationExpression:
                AnalyzeInvocationExpression(context, invocationExpression);
                break;
            default:
                Debug.Fail("Unknown node type");
                break;
        }
    }

    private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpression)
    {
        // Check if the invocation is on an event handler directly
        if (invocationExpression.Expression is not IdentifierNameSyntax identifierName)
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol;

        if (symbol == null || !IsEventSymbol(symbol))
        {
            return;
        }

        // Check if the invocation is not within an if statement or null-conditional access
        SyntaxNode? parent = invocationExpression.Parent;
        while (parent != null)
        {
            if (parent is IfStatementSyntax { Condition: BinaryExpressionSyntax binaryExpression }
                && binaryExpression.IsKind(SyntaxKind.NotEqualsExpression)
                && binaryExpression.Left is IdentifierNameSyntax binaryIdentifier
                && string.Equals(binaryIdentifier.Identifier.Text, identifierName.Identifier.Text, StringComparison.Ordinal)
                && binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                return; // Safe pattern, exit
            }

            if (parent is ConditionalAccessExpressionSyntax)
            {
                return; // Safe pattern, exit
            }

            parent = parent.Parent;
        }

        // Report a diagnostic for direct event handler invocation
        Diagnostic diagnostic = invocationExpression.GetLocation().CreateDiagnostic(Rule, identifierName.Identifier.Text);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsEventSymbol(ISymbol symbol)
    {
        return symbol is IFieldSymbol fieldSymbol && fieldSymbol.Type.Name.StartsWith("EventHandler", StringComparison.Ordinal);
    }

#pragma warning disable S125 // Sections of code should not be commented out
    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context, IfStatementSyntax ifStatement)
    {
        // Check for patterns like: if (handler != null) handler(args);
        if (ifStatement.Condition is not BinaryExpressionSyntax binaryExpression
            || !binaryExpression.IsKind(SyntaxKind.NotEqualsExpression)
            || binaryExpression.Left is not IdentifierNameSyntax identifierName
            || !binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return;
        }

        StatementSyntax statement = ifStatement.Statement;
        if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax { Expression: IdentifierNameSyntax invocationIdentifier } }
            || !string.Equals(invocationIdentifier.Identifier.Text, identifierName.Identifier.Text, StringComparison.Ordinal))
        {
            return;
        }

        Diagnostic diagnostic = ifStatement.GetLocation().CreateDiagnostic(Rule, identifierName.Identifier.Text);
        context.ReportDiagnostic(diagnostic);
    }
#pragma warning restore S125 // Sections of code should not be commented out
}

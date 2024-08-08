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
        DiagnosticSeverity.Warning,
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
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.IfStatement);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        IfStatementSyntax ifStatement = (IfStatementSyntax)context.Node;

        // Check for patterns like: if (handler != null) handler(args);
        if (ifStatement.Condition is BinaryExpressionSyntax binaryExpression &&
            binaryExpression.IsKind(SyntaxKind.NotEqualsExpression) &&
            binaryExpression.Left is IdentifierNameSyntax identifierName &&
            binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression))
        {
            StatementSyntax statement = ifStatement.Statement;
            if (statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax { Expression: IdentifierNameSyntax invocationIdentifier } }
                && string.Equals(invocationIdentifier.Identifier.Text, identifierName.Identifier.Text, StringComparison.Ordinal))
            {
                Diagnostic diagnostic = ifStatement.GetLocation().CreateDiagnostic(Rule, identifierName.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

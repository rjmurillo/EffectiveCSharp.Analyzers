namespace EffectiveCSharp.Analyzers;

/// <summary>
/// Analyzer that checks for the use of assignment statements in constructors when member initializers could be used instead.
/// </summary>
/// <seealso cref="DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferMemberInitializersToAssignmentStatementsAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.PreferMemberInitializersToAssignmentStatement;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Prefer member initializers to assignment statements",
        messageFormat: "Use a member initializer instead of an assignment statement",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field initialization in a constructor that does not use an argument should be done with a member initializer.",
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        IDictionary<string, (ExpressionStatementSyntax ExpressionStatementNode, int NumberOfAssignments)> memberInitializerCandidates = new Dictionary<string, (ExpressionStatementSyntax AssignmentNode, int NumberOfAssignments)>(StringComparer.Ordinal);

        ClassDeclarationSyntax classSyntaxNode = (ClassDeclarationSyntax)context.Node;

        // Check in every constructor if there are member initializer candidates
        foreach (ConstructorDeclarationSyntax constructor in classSyntaxNode.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            FindMemberInitializerCandidates(context, constructor, memberInitializerCandidates);
        }

        foreach (KeyValuePair<string, (ExpressionStatementSyntax ExpressionStatementNode, int NumberOfAssignments)> memberInitializerCandidate in memberInitializerCandidates)
        {
            if (memberInitializerCandidate.Value.NumberOfAssignments == 1)
            {
                Diagnostic diagnostic = memberInitializerCandidate.Value.ExpressionStatementNode.GetLocation().CreateDiagnostic(Rule);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private void FindMemberInitializerCandidates(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor, IDictionary<string, (ExpressionStatementSyntax ExpressionStatementNode, int NumberOfAssignments)> memberInitializerCandidates)
    {
        SeparatedSyntaxList<ParameterSyntax> arguments = constructor.ParameterList.Parameters;

        if (arguments.Count == 0)
        {
            // If the constructor has no arguments, we can consider all fields as candidates
            HandleEmptyArgumentsList(constructor, context.SemanticModel, memberInitializerCandidates, context.CancellationToken);
        }
        else
        {
            // If we can only add literal field assignments and invocation expressions that do not reference parameters
            // The analyzer does not support more complex scenarios such as whether the argument list contains something that depends on a parameter
            HandleArgumentsList(constructor, context.SemanticModel, memberInitializerCandidates, context.CancellationToken);
        }
    }

    private void HandleEmptyArgumentsList(ConstructorDeclarationSyntax constructor, SemanticModel semanticModel, IDictionary<string, (ExpressionStatementSyntax ExpressionStatementNode, int NumberOfAssignments)> memberInitializerCandidates, CancellationToken cancellationToken)
    {
        foreach (ExpressionStatementSyntax expressionStatement in constructor.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment
                && assignment.Left is IdentifierNameSyntax identifierName
                && semanticModel.GetSymbolInfo(identifierName, cancellationToken: cancellationToken).Symbol is IFieldSymbol fieldSymbol)
            {
                if (memberInitializerCandidates.TryGetValue(fieldSymbol.Name, out (ExpressionStatementSyntax ExpressionStatementNode, int NumberOfAssignments) existingCandidate))
                {
                    existingCandidate.NumberOfAssignments++;
                }
                else
                {
                    memberInitializerCandidates.Add(fieldSymbol.Name, (expressionStatement, 1));
                }
            }
        }
    }

    private void HandleArgumentsList(ConstructorDeclarationSyntax constructor, SemanticModel semanticModel, IDictionary<string, (ExpressionStatementSyntax ExpressionStatementNode, int NumberOfAssignments)> memberInitializerCandidates, CancellationToken cancellationToken)
    {
        foreach (ExpressionStatementSyntax expressionStatement in constructor.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment
                    && assignment.Left is IdentifierNameSyntax identifierName
                    && semanticModel.GetSymbolInfo(identifierName, cancellationToken: cancellationToken).Symbol is IFieldSymbol fieldSymbol
                    && semanticModel.GetOperation(assignment.Right, cancellationToken) is not IParameterReferenceOperation)
            {
                switch (assignment.Right)
                {
                    case LiteralExpressionSyntax:
                        {
                            if (memberInitializerCandidates.TryGetValue(fieldSymbol.Name, out (ExpressionStatementSyntax ExpressionStatementNode, int NumberOfAssignments) existingCandidate))
                            {
                                existingCandidate.NumberOfAssignments++;
                            }
                            else
                            {
                                memberInitializerCandidates.Add(fieldSymbol.Name, (expressionStatement, 1));
                            }
                        }

                        break;

                    case InvocationExpressionSyntax invocationExpressionSyntax:
                        {
                            bool canAdd = true;
                            foreach (IdentifierNameSyntax argument in invocationExpressionSyntax.ArgumentList.DescendantNodes().OfType<IdentifierNameSyntax>())
                            {
                                if (semanticModel.GetOperation(argument, cancellationToken) is IParameterReferenceOperation)
                                {
                                    canAdd = false;
                                    break;
                                }
                            }

                            if (canAdd)
                            {
                                if (memberInitializerCandidates.TryGetValue(fieldSymbol.Name, out (ExpressionStatementSyntax AssignmentNode, int NumberOfAssignments) existingCandidate))
                                {
                                    existingCandidate.NumberOfAssignments++;
                                }
                                else
                                {
                                    memberInitializerCandidates.Add(fieldSymbol.Name, (expressionStatement, 1));
                                }
                            }
                        }

                        break;

                    default:
                        continue;
                }
            }
        }
    }
}

using System.Linq.Expressions;

namespace EffectiveCSharp.Analyzers;

/// <summary>
/// Analyzer that checks for the use of assignment statements in constructors when member initializers could be used instead.
/// </summary>
/// <seealso cref="DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferMemberInitializersToAssignmentStatementsAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.PreferMemberInitializersToAssignmentStatement;

    private static readonly DiagnosticDescriptor GeneralRule = new(
        id: Id,
        title: "Prefer member initializers to assignment statements",
        messageFormat: "Use a member initializer instead of an assignment statement",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field initialization in a constructor that does not use an argument should be done with a member initializer.",
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    private static readonly DiagnosticDescriptor RuleExceptionInitializeToNullOrZero = new(
        id: Id,
        title: "Should not initialize to null or zero",
        messageFormat: "Do not initialize to null or zero as these already occur by default",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field initialization to null or zero is redundant and should be avoided.",
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    private static readonly DiagnosticDescriptor RuleExceptionShouldNotInitializeInDeclaration = new(
        id: Id,
        title: "Should not initialize in declaration due to diverging initializations in constructors",
        messageFormat: "Do not initialize a field in its declaration if you have diverging initializations in constructors. This is to prevent unnecessary allocations.",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Field initialization should not occur when there are diverging initializations in constructos. This is to prevent unnecessary allocations.",
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(GeneralRule, RuleExceptionInitializeToNullOrZero, RuleExceptionShouldNotInitializeInDeclaration);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        ClassDeclarationSyntax classSyntaxNode = (ClassDeclarationSyntax)context.Node;
        IDictionary<string, IList<ExpressionStatementSyntax>> memberInitializerCandidates = new Dictionary<string, IList<ExpressionStatementSyntax>>(StringComparer.Ordinal);
        HashSet<string> fieldToNotInitializeInDeclaration = [];

        // Check in every constructor if there are member initializer candidates
        foreach (ConstructorDeclarationSyntax constructor in classSyntaxNode.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            FindMemberInitializerCandidates(context, constructor, memberInitializerCandidates, fieldToNotInitializeInDeclaration);
        }

        // Report diagnostics on field declarations
        ReportDiagnosticsOnFieldDeclarations(context, classSyntaxNode.DescendantNodes().OfType<FieldDeclarationSyntax>(), fieldToNotInitializeInDeclaration);

        // Report diagnostics on member initializer candidates
        ReportMemberInitializerDiagnostics(context, memberInitializerCandidates);
    }

    private static bool IsConstructorParameterInUse(SyntaxNode? argumentList, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        return argumentList?
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(argument => semanticModel.GetOperation(argument, cancellationToken) is IParameterReferenceOperation) ?? false;
    }

    private static bool IsConstructorParameterInUse(SeparatedSyntaxList<ExpressionSyntax> expressions, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        foreach (ExpressionSyntax expression in expressions)
        {
            if (expression is IdentifierNameSyntax argument
                && semanticModel.GetOperation(argument, cancellationToken) is IParameterReferenceOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportDiagnosticsOnFieldDeclarations(SyntaxNodeAnalysisContext context, IEnumerable<FieldDeclarationSyntax> fields, ISet<string> fieldToNotInitializeInDeclaration)
    {
        foreach (FieldDeclarationSyntax field in fields)
        {
            // Check and report fields that are initialized to null or zero. Structs are also checked for empty initializers.
            if (IsFieldNullOrZero(field) || IsStructInitializerEmpty(context, field))
            {
                context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleExceptionInitializeToNullOrZero));
            }

            if (fieldToNotInitializeInDeclaration.Contains(field.Declaration.Variables[0].Identifier.Text))
            {
                context.ReportDiagnostic(field.GetLocation().CreateDiagnostic(RuleExceptionShouldNotInitializeInDeclaration));
            }
        }
    }

    private static bool IsFieldNullOrZero(FieldDeclarationSyntax field)
    {
        IEnumerable<LiteralExpressionSyntax> literalExpressionEnumerable = field.DescendantNodes().OfType<LiteralExpressionSyntax>();
        return literalExpressionEnumerable.Count() == 1 && (literalExpressionEnumerable.Single().Token.Value is null or 0);
    }

    private static bool IsStructInitializerEmpty(SyntaxNodeAnalysisContext context, FieldDeclarationSyntax field)
    {
        if (context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type?.TypeKind == TypeKind.Struct)
        {
            IEnumerable<ObjectCreationExpressionSyntax> objectCreationNodeEnumerable = field.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            return objectCreationNodeEnumerable.Count() == 1 && objectCreationNodeEnumerable.Single().ArgumentList?.Arguments.Count == 0;
        }

        return false;
    }

    private static void ReportMemberInitializerDiagnostics(SyntaxNodeAnalysisContext context, IDictionary<string, IList<ExpressionStatementSyntax>> memberInitializerCandidates)
    {
        foreach (IList<ExpressionStatementSyntax> memberInitializerCandidateList in memberInitializerCandidates.Values)
        {
            foreach (ExpressionStatementSyntax memberInitializerCandidate in memberInitializerCandidateList)
            {
                Diagnostic diagnostic = memberInitializerCandidate.GetLocation().CreateDiagnostic(GeneralRule);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void FindMemberInitializerCandidates(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor, IDictionary<string, IList<ExpressionStatementSyntax>> memberInitializerCandidates, ISet<string> fieldToNotInitializeInDeclaration)
    {
        SeparatedSyntaxList<ParameterSyntax> arguments = constructor.ParameterList.Parameters;

        if (arguments.Count == 0)
        {
            // If the constructor has no arguments, we can consider all fields as candidates
            HandleEmptyArgumentsList(constructor, context.SemanticModel, memberInitializerCandidates, fieldToNotInitializeInDeclaration, context.CancellationToken);
        }
        else
        {
            // If we can only add literal field assignments and invocation expressions that do not reference parameters
            // The analyzer does not support more complex scenarios such as whether the argument list contains something that depends on a parameter
            HandleArgumentsList(constructor, context.SemanticModel, memberInitializerCandidates, fieldToNotInitializeInDeclaration, context.CancellationToken);
        }
    }

    private static void HandleEmptyArgumentsList(ConstructorDeclarationSyntax constructor, SemanticModel semanticModel, IDictionary<string, IList<ExpressionStatementSyntax>> memberInitializerCandidates, ISet<string> fieldToNotInitializeInDeclaration, CancellationToken cancellationToken)
    {
        foreach (ExpressionStatementSyntax expressionStatement in constructor.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment
                && assignment.Left is IdentifierNameSyntax identifierName
                && semanticModel.GetSymbolInfo(identifierName, cancellationToken: cancellationToken).Symbol is IFieldSymbol fieldSymbol
                && !fieldToNotInitializeInDeclaration.Contains(fieldSymbol.Name))
            {
                ProcessMemberInitializerCandidates(
                    fieldSymbol.Name,
                    assignment,
                    expressionStatement,
                    memberInitializerCandidates,
                    fieldToNotInitializeInDeclaration);
            }
        }
    }

    private static void HandleArgumentsList(ConstructorDeclarationSyntax constructor, SemanticModel semanticModel, IDictionary<string, IList<ExpressionStatementSyntax>> memberInitializerCandidates, ISet<string> fieldToNotInitializeInDeclaration, CancellationToken cancellationToken)
    {
        foreach (ExpressionStatementSyntax expressionStatement in constructor.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment
                    && assignment.Left is IdentifierNameSyntax identifierName
                    && semanticModel.GetSymbolInfo(identifierName, cancellationToken: cancellationToken).Symbol is IFieldSymbol fieldSymbol
                    && !fieldToNotInitializeInDeclaration.Contains(fieldSymbol.Name)
                    && semanticModel.GetOperation(assignment.Right, cancellationToken) is not IParameterReferenceOperation)
            {
                if (ShouldProcessMemberInitializer(assignment.Right, semanticModel, cancellationToken))
                {
                    ProcessMemberInitializerCandidates(
                        fieldSymbol.Name,
                        assignment,
                        expressionStatement,
                        memberInitializerCandidates,
                        fieldToNotInitializeInDeclaration);
                }
                else if (memberInitializerCandidates.ContainsKey(fieldSymbol.Name))
                {
                    fieldToNotInitializeInDeclaration.Add(fieldSymbol.Name);
                    memberInitializerCandidates.Remove(fieldSymbol.Name);
                }
            }
        }
    }

    private static bool ShouldProcessMemberInitializer(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        return expression switch
        {
            LiteralExpressionSyntax => true,
            InvocationExpressionSyntax invocationExpressionSyntax => !IsConstructorParameterInUse(invocationExpressionSyntax.ArgumentList, semanticModel, cancellationToken),
            ObjectCreationExpressionSyntax objectCreationExpressionSyntax => !IsConstructorParameterInUse(objectCreationExpressionSyntax.ArgumentList, semanticModel, cancellationToken),
            ImplicitObjectCreationExpressionSyntax implicitObjectCreationExpressionSyntax => !IsConstructorParameterInUse(implicitObjectCreationExpressionSyntax.ArgumentList, semanticModel, cancellationToken),
            InitializerExpressionSyntax initializerExpressionSyntax => !IsConstructorParameterInUse(initializerExpressionSyntax.Expressions, semanticModel, cancellationToken),
            _ => false,
        };
    }

    private static void ProcessMemberInitializerCandidates(
        string fieldName,
        AssignmentExpressionSyntax assignment,
        ExpressionStatementSyntax expressionStatement,
        IDictionary<string, IList<ExpressionStatementSyntax>> memberInitializerCandidates,
        ISet<string> fieldToNotInitializeInDeclaration)
    {
        if (memberInitializerCandidates.TryGetValue(fieldName, out IList<ExpressionStatementSyntax> existingCandidates))
        {
            if (existingCandidates.Count == 0)
            {
                throw new InvalidOperationException("The list of existing candidates should never be empty when we are able to access it!");
            }

            if (((AssignmentExpressionSyntax)existingCandidates[0].Expression).Right.IsEquivalentTo(assignment.Right))
            {
                existingCandidates.Add(expressionStatement);
            }
            else
            {
                fieldToNotInitializeInDeclaration.Add(fieldName);
                memberInitializerCandidates.Remove(fieldName);
            }
        }
        else
        {
            memberInitializerCandidates.Add(fieldName, new List<ExpressionStatementSyntax>() { expressionStatement });
        }
    }
}

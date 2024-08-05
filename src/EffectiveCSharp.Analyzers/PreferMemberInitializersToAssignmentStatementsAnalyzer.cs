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
        IDictionary<string, FieldInitializationInfo> fieldInitializationInfo = GetAllFieldInitializationInformation(classSyntaxNode, context.SemanticModel, context.CancellationToken);

        // Check in every constructor if there are member initializer candidates
        foreach (ConstructorDeclarationSyntax constructor in classSyntaxNode.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            FindMemberInitializerCandidates(context, constructor, fieldInitializationInfo);
        }

        // Report diagnostics on field declarations
        ReportDiagnosticsOnFieldDeclarations(context, fieldInitializationInfo);
    }

    private static IDictionary<string, FieldInitializationInfo> GetAllFieldInitializationInformation(ClassDeclarationSyntax classSyntaxNode, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        IDictionary<string, FieldInitializationInfo> fieldInitializationInfo = new Dictionary<string, FieldInitializationInfo>(StringComparer.Ordinal);

        foreach (FieldDeclarationSyntax field in classSyntaxNode.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                string? fieldName = semanticModel.GetDeclaredSymbol(variable, cancellationToken)?.Name;

                if (fieldName is not null)
                {
                    bool hasInitializer = variable.Initializer is not null;
                    fieldInitializationInfo.Add(fieldName, new FieldInitializationInfo(fieldName, field, new List<ExpressionStatementSyntax>(), shouldNotInitializeInDeclaration: false, fieldHasInitializer: hasInitializer));
                }
            }
        }

        return fieldInitializationInfo;
    }

    private static void ReportDiagnosticsOnFieldDeclarations(SyntaxNodeAnalysisContext context, IDictionary<string, FieldInitializationInfo> fields)
    {
        foreach (FieldInitializationInfo field in fields.Values)
        {
            // Check and report fields that are initialized to null or zero. Structs are also checked for empty initializers.
            if (IsFieldNullOrZero(field) || IsStructInitializerEmpty(context, field))
            {
                context.ReportDiagnostic(field.FieldDeclaration.GetLocation().CreateDiagnostic(RuleExceptionInitializeToNullOrZero));
            }

            if (field.ShouldNotInitializeInDeclaration)
            {
                context.ReportDiagnostic(field.FieldDeclaration.GetLocation().CreateDiagnostic(RuleExceptionShouldNotInitializeInDeclaration));
            }

            if (field.MemberInitializers.Count == 0 && !field.ShouldNotInitializeInDeclaration && !field.FieldHasInitializer)
            {
                context.ReportDiagnostic(field.FieldDeclaration.GetLocation().CreateDiagnostic(GeneralRule));
            }
            else
            {
                foreach (ExpressionStatementSyntax memberInitializer in field.MemberInitializers)
                {
                    context.ReportDiagnostic(memberInitializer.GetLocation().CreateDiagnostic(GeneralRule));
                }
            }
        }
    }

    private static bool IsFieldNullOrZero(FieldInitializationInfo field)
    {
        IEnumerable<LiteralExpressionSyntax> literalExpressionEnumerable = field.FieldDeclaration.DescendantNodes().OfType<LiteralExpressionSyntax>();
        return literalExpressionEnumerable.Count() == 1 && (literalExpressionEnumerable.Single().Token.Value is null or 0);
    }

    private static bool IsStructInitializerEmpty(SyntaxNodeAnalysisContext context, FieldInitializationInfo field)
    {
        if (context.SemanticModel.GetTypeInfo(field.FieldDeclaration.Declaration.Type, context.CancellationToken).Type?.TypeKind == TypeKind.Struct)
        {
            IEnumerable<ObjectCreationExpressionSyntax> objectCreationNodeEnumerable = field.FieldDeclaration.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            return objectCreationNodeEnumerable.Count() == 1 && objectCreationNodeEnumerable.Single().ArgumentList?.Arguments.Count == 0;
        }

        return false;
    }

    private static void FindMemberInitializerCandidates(SyntaxNodeAnalysisContext context, ConstructorDeclarationSyntax constructor, IDictionary<string, FieldInitializationInfo> fields)
    {
        SeparatedSyntaxList<ParameterSyntax> arguments = constructor.ParameterList.Parameters;

        if (arguments.Count == 0)
        {
            // If the constructor has no arguments, we can consider all fields as candidates
            HandleEmptyArgumentsList(constructor, context, fields);
        }
        else
        {
            // If we can only add literal field assignments and invocation expressions that do not reference parameters
            // The analyzer does not support more complex scenarios such as whether the argument list contains something that depends on a parameter
            HandleArgumentsList(constructor, context, fields);
        }
    }

    private static void HandleEmptyArgumentsList(ConstructorDeclarationSyntax constructor, SyntaxNodeAnalysisContext context, IDictionary<string, FieldInitializationInfo> fields)
    {
        foreach (ExpressionStatementSyntax expressionStatement in constructor.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment
                && assignment.Left is IdentifierNameSyntax identifierName
                && context.SemanticModel.GetSymbolInfo(identifierName, cancellationToken: context.CancellationToken).Symbol is IFieldSymbol fieldSymbol
                && fields.TryGetValue(fieldSymbol.Name, out FieldInitializationInfo fieldInitializationInfo)
                && !fieldInitializationInfo.ShouldNotInitializeInDeclaration)
            {
                ProcessMemberInitializerCandidates(
                    assignment,
                    expressionStatement,
                    fieldInitializationInfo);
            }
        }
    }

    private static void HandleArgumentsList(ConstructorDeclarationSyntax constructor, SyntaxNodeAnalysisContext context, IDictionary<string, FieldInitializationInfo> fields)
    {
        foreach (ExpressionStatementSyntax expressionStatement in constructor.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (expressionStatement.Expression is AssignmentExpressionSyntax assignment
                    && assignment.Left is IdentifierNameSyntax identifierName
                    && context.SemanticModel.GetSymbolInfo(identifierName, cancellationToken: context.CancellationToken).Symbol is IFieldSymbol fieldSymbol
                    && fields.TryGetValue(fieldSymbol.Name, out FieldInitializationInfo fieldInitializationInfo)
                    && !fieldInitializationInfo.ShouldNotInitializeInDeclaration
                    && context.SemanticModel.GetOperation(assignment.Right, context.CancellationToken) is not IParameterReferenceOperation)
            {
                if (!IsConstructorParameterInUse(assignment.Right, context))
                {
                    ProcessMemberInitializerCandidates(
                        assignment,
                        expressionStatement,
                        fieldInitializationInfo);
                }
                else
                {
                    fieldInitializationInfo.ShouldNotInitializeInDeclaration = true;
                    fieldInitializationInfo.MemberInitializers.Clear();
                }
            }
        }
    }

    private static bool IsConstructorParameterInUse(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        return expression switch
        {
            LiteralExpressionSyntax => true,
            InvocationExpressionSyntax invocationExpressionSyntax => IsConstructorParameterInUse(invocationExpressionSyntax.ArgumentList, context),
            ObjectCreationExpressionSyntax objectCreationExpressionSyntax => IsConstructorParameterInUse(objectCreationExpressionSyntax.ArgumentList, context),
            ImplicitObjectCreationExpressionSyntax implicitObjectCreationExpressionSyntax => IsConstructorParameterInUse(implicitObjectCreationExpressionSyntax.ArgumentList, context),
            InitializerExpressionSyntax initializerExpressionSyntax => IsConstructorParameterInUse(initializerExpressionSyntax.Expressions, context),
            _ => false,
        };
    }

    private static bool IsConstructorParameterInUse(SyntaxNode? argumentList, SyntaxNodeAnalysisContext context)
    {
        return argumentList?
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(argument => context.SemanticModel.GetOperation(argument, context.CancellationToken) is IParameterReferenceOperation) ?? false;
    }

    private static bool IsConstructorParameterInUse(SeparatedSyntaxList<ExpressionSyntax> expressions, SyntaxNodeAnalysisContext context)
    {
        foreach (ExpressionSyntax expression in expressions)
        {
            if (expression is IdentifierNameSyntax argument
                && context.SemanticModel.GetOperation(argument, context.CancellationToken) is IParameterReferenceOperation)
            {
                return true;
            }
        }

        return false;
    }

    private static void ProcessMemberInitializerCandidates(
        AssignmentExpressionSyntax assignment,
        ExpressionStatementSyntax expressionStatement,
        FieldInitializationInfo fieldInitializationInfo)
    {
        IList<ExpressionStatementSyntax> memberInitializersInConstructor = fieldInitializationInfo.MemberInitializers;

        if (memberInitializersInConstructor.Count == 0 && !fieldInitializationInfo.ShouldNotInitializeInDeclaration)
        {
            memberInitializersInConstructor.Add(expressionStatement);
        }
        else if (((AssignmentExpressionSyntax)memberInitializersInConstructor[0].Expression).Right.IsEquivalentTo(assignment.Right))
        {
            memberInitializersInConstructor.Add(expressionStatement);
        }
        else
        {
            fieldInitializationInfo.ShouldNotInitializeInDeclaration = true;
            fieldInitializationInfo.MemberInitializers.Clear();
        }
    }

    private sealed record FieldInitializationInfo
    {
        public FieldInitializationInfo(
            string fieldName,
            FieldDeclarationSyntax fieldDeclaration,
            IList<ExpressionStatementSyntax> memberInitializers,
            bool shouldNotInitializeInDeclaration,
            bool fieldHasInitializer)
        {
            FieldName = fieldName;
            FieldDeclaration = fieldDeclaration;
            MemberInitializers = memberInitializers;
            ShouldNotInitializeInDeclaration = shouldNotInitializeInDeclaration;
            FieldHasInitializer = fieldHasInitializer;
        }

        public string FieldName { get; init; }

        public FieldDeclarationSyntax FieldDeclaration { get; init; }

        public IList<ExpressionStatementSyntax> MemberInitializers { get; init; }

        public bool ShouldNotInitializeInDeclaration { get; set; }

        public bool FieldHasInitializer { get; init; }
    }
}

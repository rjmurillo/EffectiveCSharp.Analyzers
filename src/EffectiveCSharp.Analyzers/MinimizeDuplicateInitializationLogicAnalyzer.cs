namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #14 - Minimize duplicate initialization logic.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MinimizeDuplicateInitializationLogicAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title = "Minimize duplicate initialization logic";

    private static readonly LocalizableString MessageFormat =
        "Constructor '{0}' contains duplicate initialization logic";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.MinimizeDuplicateInitializationLogic,
        Title,
        MessageFormat,
        Categories.Initialization,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/rules/{DiagnosticIds.MinimizeDuplicateInitializationLogic}.md");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        ConstructorDeclarationSyntax constructor = (ConstructorDeclarationSyntax)context.Node;

        // Skip if the constructor uses chaining
        if (constructor.Initializer != null)
        {
            return;
        }

        if (constructor.Parent is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        SemanticModel semanticModel = context.SemanticModel;

        // Collect other constructors that do not use chaining
        List<ConstructorDeclarationSyntax> constructors = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c != constructor && c.Initializer == null)
            .ToList();

        List<InitializationStatement> currentInitStatements = GetInitializationStatements(constructor, semanticModel, context.CancellationToken);

        // Skip if no initialization statements are found
        if (currentInitStatements.Count == 0)
        {
            return;
        }

        foreach (ConstructorDeclarationSyntax? otherConstructor in constructors)
        {
            List<InitializationStatement> otherInitStatements = GetInitializationStatements(otherConstructor, semanticModel, context.CancellationToken);

            if (otherInitStatements.Count == 0)
            {
                continue;
            }

            // Compare the sets of initialization statements
            if (!InitializationStatementsAreEqual(currentInitStatements, otherInitStatements))
            {
                continue;
            }

            Diagnostic diagnostic = constructor.Identifier.GetLocation().CreateDiagnostic(Rule, constructor.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
            break;
        }
    }

    private static List<InitializationStatement> GetInitializationStatements(
        ConstructorDeclarationSyntax constructor,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        List<InitializationStatement> statements = [];

        if (constructor.Body == null)
        {
            return statements;
        }

        foreach (StatementSyntax statement in constructor.Body.Statements)
        {
            switch (statement)
            {
                // Handle assignments and method calls
                case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }:
                    {
                        ISymbol? leftSymbol = semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol;
                        ISymbol? rightSymbol = semanticModel.GetSymbolInfo(assignment.Right, cancellationToken).Symbol ??
                                               semanticModel.GetTypeInfo(assignment.Right, cancellationToken).Type;

                        if (leftSymbol != null && rightSymbol != null)
                        {
                            statements.Add(
                                new InitializationStatement(
                                    kind: InitializationKind.Assignment,
                                    leftSymbol: leftSymbol,
                                    rightSymbol: rightSymbol));
                        }

                        break;
                    }

                case ExpressionStatementSyntax exprStmt:
                    {
                        if (exprStmt.Expression is InvocationExpressionSyntax invocation &&
                            semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol methodSymbol)
                        {
                            statements.Add(
                                new InitializationStatement(
                                    kind: InitializationKind.MethodCall,
                                    methodSymbol: methodSymbol));
                        }

                        break;
                    }

                case LocalDeclarationStatementSyntax localDecl:
                    {
                        foreach (VariableDeclaratorSyntax variable in localDecl.Declaration.Variables)
                        {
                            if (variable.Initializer == null)
                            {
                                continue;
                            }

                            ISymbol? symbol = semanticModel.GetDeclaredSymbol(variable, cancellationToken);
                            ITypeSymbol? initializerType = semanticModel.GetTypeInfo(variable.Initializer.Value, cancellationToken).Type;
                            if (symbol != null && initializerType != null)
                            {
                                statements.Add(
                                    new InitializationStatement(
                                        kind: InitializationKind.VariableDeclaration,
                                        leftSymbol: symbol,
                                        rightSymbol: initializerType));
                            }
                        }

                        break;
                    }
            }
        }

        return statements;
    }

    private static bool InitializationStatementsAreEqual(
        IEnumerable<InitializationStatement> first,
        IEnumerable<InitializationStatement> second)
    {
        HashSet<InitializationStatement> firstSet = [.. first];
        HashSet<InitializationStatement> secondSet = [.. second];

        return firstSet.SetEquals(secondSet) && firstSet.Count > 0;
    }

    private enum InitializationKind
    {
        Assignment,
        MethodCall,
        VariableDeclaration,
    }

    private sealed class InitializationStatement : IEquatable<InitializationStatement>
    {
        private InitializationStatement(InitializationKind kind)
        {
            Kind = kind;
        }

        public InitializationStatement(InitializationKind kind, ISymbol leftSymbol, ISymbol rightSymbol)
            : this(kind)
        {
            LeftSymbol = leftSymbol;
            RightSymbol = rightSymbol;
        }

        public InitializationStatement(InitializationKind kind, IMethodSymbol methodSymbol)
            : this(kind)
        {
            MethodSymbol = methodSymbol;
        }

        public InitializationKind Kind { get; }

        public ISymbol? LeftSymbol { get; }

        public ISymbol? RightSymbol { get; }

        public IMethodSymbol? MethodSymbol { get; }

        public bool Equals(InitializationStatement? other)
        {
            if (other == null || Kind != other.Kind)
            {
                return false;
            }

            SymbolEqualityComparer comparer = SymbolEqualityComparer.Default;

            return Kind switch
            {
                InitializationKind.Assignment => comparer.Equals(LeftSymbol, other.LeftSymbol) &&
                                                 comparer.Equals(RightSymbol, other.RightSymbol),
                InitializationKind.MethodCall => comparer.Equals(MethodSymbol, other.MethodSymbol),
                InitializationKind.VariableDeclaration => comparer.Equals(LeftSymbol, other.LeftSymbol) &&
                                                          comparer.Equals(RightSymbol, other.RightSymbol),
                _ => false,
            };
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as InitializationStatement);
        }

        public override int GetHashCode()
        {
            HashCode hashCode = new();
            hashCode.Add(Kind);
            SymbolEqualityComparer comparer = SymbolEqualityComparer.IncludeNullability;

            switch (Kind)
            {
                case InitializationKind.Assignment:
                case InitializationKind.VariableDeclaration:
                    hashCode.Add(comparer.GetHashCode(LeftSymbol));
                    hashCode.Add(comparer.GetHashCode(RightSymbol));
                    break;
                case InitializationKind.MethodCall:
                    hashCode.Add(comparer.GetHashCode(MethodSymbol));
                    break;
            }

            return hashCode.ToHashCode();
        }
    }
}

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
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

        // Collect constructors that are instance constructors and do not use chaining
        var constructors = namedTypeSymbol.Constructors
            .Where(c => !c.IsStatic && c.DeclaringSyntaxReferences.Length > 0)
            .Select(c => new
            {
                ConstructorSymbol = c,
                Declaration = c.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) as ConstructorDeclarationSyntax,
            })
            .Where(c => c.Declaration is { Initializer: null })
            .ToList();

        if (constructors.Count < 2)
        {
            return;
        }

        SyntaxTree? syntaxTree = constructors[0].Declaration?.SyntaxTree;
        if (syntaxTree == null)
        {
            return;
        }

#pragma warning disable RS1030
        SemanticModel semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
#pragma warning restore RS1030

        // Compute initialization statements for all constructors once
        Dictionary<IMethodSymbol, List<InitializationStatement>> constructorInitStatements = new(SymbolEqualityComparer.IncludeNullability);

        for (int i = 0; i < constructors.Count; i++)
        {
            var ctor = constructors[i];
            List<InitializationStatement> initStatements = GetInitializationStatements(
                ctor.Declaration,
                semanticModel,
                context.CancellationToken);

            if (initStatements.Count > 0)
            {
                constructorInitStatements[ctor.ConstructorSymbol] = initStatements;
            }
        }

        // Compare initialization statements between constructors
        foreach (KeyValuePair<IMethodSymbol, List<InitializationStatement>> ctor in constructorInitStatements)
        {
            foreach (KeyValuePair<IMethodSymbol, List<InitializationStatement>> otherCtor in constructorInitStatements)
            {
                if (ctor.Key.Equals(otherCtor.Key, SymbolEqualityComparer.Default))
                {
                    continue;
                }

                if (!InitializationStatementsAreEqual(ctor.Value, otherCtor.Value))
                {
                    continue;
                }

                Diagnostic diagnostic = ctor.Key.Locations[0].CreateDiagnostic(Rule, ctor.Key.Name);
                context.ReportDiagnostic(diagnostic);
                break;
            }
        }
    }

    private static List<InitializationStatement> GetInitializationStatements(
        ConstructorDeclarationSyntax? constructor,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        List<InitializationStatement> statements = [];

        if (constructor?.Body == null)
        {
            return statements;
        }

        for (int i = 0; i < constructor.Body.Statements.Count; i++)
        {
            StatementSyntax statement = constructor.Body.Statements[i];
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
                            ITypeSymbol? initializerType = semanticModel.GetTypeInfo(
                                variable.Initializer.Value,
                                cancellationToken).Type;
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
        List<InitializationStatement> first,
        List<InitializationStatement> second)
    {
        if (first.Count != second.Count || first.Count == 0)
        {
            return false;
        }

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
            HashCode hashCode = default;
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

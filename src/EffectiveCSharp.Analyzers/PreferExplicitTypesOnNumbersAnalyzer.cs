using System;
using System.Collections.Generic;
using System.Text;

namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="DiagnosticAnalyzer"/> for Effective C# Item #1 - Prefer implicit types except on numbers.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer" />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferExplicitTypesOnNumbersAnalyzer : DiagnosticAnalyzer
{
    private const string Id = DiagnosticIds.PreferImplicitlyTypedLocalVariables;

    private static readonly DiagnosticDescriptor Rule = new(
        id: Id,
        title: "Prefer implicitly typed local variables",
        description: "Use var to declare local variables for better readability and efficiency, except for built-in numeric types where explicit typing prevents potential conversion issues.",
        messageFormat: "",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: $"https://github.com/rjmurillo/EffectiveCSharp.Analyzers/blob/{ThisAssembly.GitCommitId}/docs/{Id}.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.LocalDeclarationStatement);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        LocalDeclarationStatementSyntax localDeclaration = (LocalDeclarationStatementSyntax)context.Node;

        // Ensure the variable is declared using 'var'
        if (!localDeclaration.Declaration.Type.IsVar)
        {
            return;
        }

        VariableDeclaratorSyntax variable = localDeclaration.Declaration.Variables.First();
        ExpressionSyntax? initializer = variable.Initializer?.Value;

        if (initializer is null)
        {
            return;
        }

        TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(initializer, context.CancellationToken);
        ITypeSymbol? type = typeInfo.ConvertedType;

        if (type is null)
        {
            return;
        }

        // Check if the type is a numeric type
        if (type.SpecialType == SpecialType.System_Int32 ||
            type.SpecialType == SpecialType.System_Int64 ||
            type.SpecialType == SpecialType.System_Single ||
            type.SpecialType == SpecialType.System_Double ||
            type.SpecialType == SpecialType.System_Decimal)
        {
            Diagnostic diagnostic = localDeclaration.GetLocation().CreateDiagnostic(Rule);
            context.ReportDiagnostic(diagnostic);
        }
    }
}

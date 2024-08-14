namespace EffectiveCSharp.Analyzers;

/// <summary>
/// A <see cref="CodeFixProvider"/> that provides a code fix for the <see cref="FormattableStringForCultureSpecificStringsAnalyzer"/>.
/// </summary>
/// <seealso cref="Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider" />
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormattableStringForCultureSpecificStringsCodeFixProvider))]
[Shared]
public class FormattableStringForCultureSpecificStringsCodeFixProvider : CodeFixProvider
{
    private static readonly string Title = "Use string.Create or FormattableString";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.PreferFormattableStringForCultureSpecificStrings);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (semanticModel == null)
        {
            return;
        }

        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];
        TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the interpolated string expression identified by the diagnostic
        InterpolatedStringExpressionSyntax? interpolatedString = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<InterpolatedStringExpressionSyntax>().First();

        if (interpolatedString == null)
        {
            return;
        }

        Compilation compilation = semanticModel.Compilation;

        (Version? dotNetVersion, _, LanguageVersion? compilerLanguageVersion) = compilation.GetVersions();
        if (compilerLanguageVersion is null || dotNetVersion is null)
        {
            return;
        }

        // REVIEW: A similar version of this logic is in the analyzer as well
        switch (compilerLanguageVersion)
        {
            // string.Create was introduced in C# 10 and .NET 6
            // .NET 6+, favor `string.Create`
            case >= LanguageVersion.CSharp10 when dotNetVersion >= DotNet.Versions.DotNet6:

            // Pre-.NET 6, favor FormattableString
            case >= LanguageVersion.CSharp9 when dotNetVersion >= DotNet.Versions.DotNet5:
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedSolution: c => UseCultureSpecificStringAsync(context.Document, semanticModel, interpolatedString, c),
                        equivalenceKey: Title),
                    diagnostic);
                break;
        }
    }

    private static async Task<Solution> UseCultureSpecificStringAsync(
        Document document,
        SemanticModel semanticModel,
        InterpolatedStringExpressionSyntax interpolatedString,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root == null)
        {
            return document.Project.Solution;
        }

        Compilation compilation = semanticModel.Compilation;

        (Version? dotNetVersion, _, LanguageVersion? compilerLanguageVersion) = compilation.GetVersions();
        if (compilerLanguageVersion is null || dotNetVersion is null)
        {
            return document.Project.Solution;
        }

        ExpressionSyntax? newExpression = null;

        // REVIEW: A similar version of this logic is in the analyzer as well
        switch (compilerLanguageVersion)
        {
            // string.Create was introduced in C# 10 and .NET 6
            // .NET 6+, favor `string.Create`
            case >= LanguageVersion.CSharp10 when dotNetVersion >= DotNet.Versions.DotNet6:

                newExpression = CreateStringCreateExpression(interpolatedString);
                break;

            // Pre-.NET 6, favor FormattableString
            case >= LanguageVersion.CSharp9 when dotNetVersion >= DotNet.Versions.DotNet5:
                newExpression = CreateFormattableStringWithCurrentCultureExpression(interpolatedString);
                break;
        }

        if (newExpression == null)
        {
            return document.Project.Solution;
        }

        SyntaxNode newRoot = root.ReplaceNode(interpolatedString, newExpression);

        return document.WithSyntaxRoot(newRoot).Project.Solution;
    }

    private static InvocationExpressionSyntax CreateStringCreateExpression(InterpolatedStringExpressionSyntax interpolatedString)
    {
        // Replace with string.Create(CultureInfo.CurrentCulture, ...) for .NET 6 and later
        ArgumentListSyntax arguments = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(
                new[]
                {
                    SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("CultureInfo"), SyntaxFactory.IdentifierName("CurrentCulture"))),
                    SyntaxFactory.Argument(interpolatedString),
                }));

        InvocationExpressionSyntax invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)), SyntaxFactory.IdentifierName("Create")),
            arguments);

        return invocation;
    }

    private static InvocationExpressionSyntax CreateFormattableStringWithCurrentCultureExpression(InterpolatedStringExpressionSyntax interpolatedString)
    {
        // Replace with FormattableString.CurrentCulture(...) just to be explicit about what was happening before
        InvocationExpressionSyntax invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("FormattableString"), SyntaxFactory.IdentifierName("CurrentCulture")),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Argument(interpolatedString) })));

        return invocation;
    }
}

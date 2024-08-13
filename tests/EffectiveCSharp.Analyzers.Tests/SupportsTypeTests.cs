using EffectiveCSharp.Analyzers.Common;
using Microsoft.CodeAnalysis;

namespace EffectiveCSharp.Analyzers.Tests;

public class SupportsTypeTests
{
    [Fact]
    public void Supports_FormattableString()
    {
        Compilation compilation = CreateCompilation();

        bool result = compilation.SupportsType("System.FormattableString");

        Assert.True(result);
    }

    [Fact]
    public void Supports_StringCreate()
    {
        Compilation compilation = CreateCompilation();

        bool result = compilation.SupportsType("System.String", "Create");

        Assert.True(result);
    }

    private static Compilation CreateCompilation()
    {
        const string testCode = """
                                public class C
                                {
                                    public void M()
                                    {
                                    }
                                }
                                """;

        Compilation compilation = CreateCompilation(testCode);
        return compilation;
    }

    private static Compilation CreateCompilation(string sourceCode)
    {
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(sourceCode) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}

using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.Tests.CompositeAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

#pragma warning disable MA0048  // There are multiple types of tests here, keeping them in the same file
#pragma warning disable SA1649
#pragma warning disable SA1402
#pragma warning disable SA1001  // The harness has literal code as a string, which can be weirdly formatted
#pragma warning disable SA1113
#pragma warning disable SA1115

public class HeapAllocationTests
{
    [Fact]
    public async Task Value_Type_Member_of_a_Reference_Type()
    {
        await Verifier.VerifyAnalyzerAsync(
            @"
public class Container
{
  public int ValueField;
}
"
            ,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Boxing()
    {
        await Verifier.VerifyAnalyzerAsync(
            @"
public class Container
{
  public void BoxingExample()
  {
    int i = 42;
    object boxed = {|ECS0900:i|};
  }
}
"
            ,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Struct_with_Value_Type_on_Heap()
    {
        await Verifier.VerifyAnalyzerAsync(
            @"
public class Container
{
  public MyStruct StructField;

  public struct MyStruct
  {
    public int ValueField;
  }
}
"
            ,
            ReferenceAssemblyCatalog.Net80);
    }
}

public class StackAllocationTests
{
    [Fact]
    public async Task Value_Type_as_Local_Variable()
    {
        await Verifier.VerifyAnalyzerAsync(
            @"
public class Container
{
  public void LocalVariableExample()
  {
    int i = 42;
  }
}
"
            ,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Value_Type_as_Parameter()
    {
        await Verifier.VerifyAnalyzerAsync(
            @"
public class Container
{
  public void LocalVariableExample(int parameterValue)
  {    
  }
}
"
            ,
            ReferenceAssemblyCatalog.Net80);
    }

    [Fact]
    public async Task Ref_Struct()
    {
        await Verifier.VerifyAnalyzerAsync(
            @"
public class Container
{
  public void RefStructExample()
  {
    MyRefStruct localRefStruct;
  }
}

public ref struct MyRefStruct
{
  public int ValueField;
}
"
            ,
            ReferenceAssemblyCatalog.Net80);
    }
}

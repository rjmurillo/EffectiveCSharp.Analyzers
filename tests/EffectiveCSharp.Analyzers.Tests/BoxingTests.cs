using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.MinimizeBoxingUnboxingAnalyzer>;

namespace EffectiveCSharp.Analyzers.Tests;

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
    object boxed = {|ECS0009:i|};
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

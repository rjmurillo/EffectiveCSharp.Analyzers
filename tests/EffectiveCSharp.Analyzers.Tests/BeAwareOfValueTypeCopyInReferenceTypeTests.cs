using Verifier = EffectiveCSharp.Analyzers.Tests.Helpers.AnalyzerVerifier<EffectiveCSharp.Analyzers.BeAwareOfValueTypeCopyInReferenceTypes>;

namespace EffectiveCSharp.Analyzers.Tests;

public class BeAwareOfValueTypeCopyInReferenceTypeTests
{
    [Fact]
    public async Task Analyzer()
    {
        // We need to ensure the detection of boxing in cases where a value type
        // is being assigned or accessed in a way that results in copy semantics
        //
        // In this case, we are copying the value type when we bring it in and out
        // the reference type List<Person>.
        await Verifier.VerifyAnalyzerAsync(
            @"
internal class Program
{
  static void Main()
  {

    // Using the Person in a collection
    var attendees = new List<Person>();
    var p = new Person { Name = ""Old Name"" };
    attendees.Add(p);

    // Try to change the name
    var p2 = {|ECS0009:attendees[0]|};
    p2.Name = ""New Name"";

    // Writes ""Old Name"" because we pulled a copy of the struct
    Console.WriteLine({|ECS0009:attendees[0]|}.ToString());
  }
}

public struct Person
{
  public string Name { get; set; }
  public override string ToString() => Name;
}
"
            ,
            ReferenceAssemblyCatalog.Net80);
    }
}

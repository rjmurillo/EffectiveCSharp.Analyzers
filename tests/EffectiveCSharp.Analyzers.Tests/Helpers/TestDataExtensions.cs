namespace EffectiveCSharp.Analyzers.Tests.Helpers;

internal static class TestDataExtensions
{
    public static TheoryData<string, string> WithReferenceAssemblyGroups(this TheoryData<string> data)
    {
        TheoryData<string, string> retVal = new();

        foreach (object[]? item in data)
        {
            foreach (object f in item)
            {
                retVal.Add(ReferenceAssemblyCatalog.Net80, (string)f);
            }
        }

        return retVal;
    }
}

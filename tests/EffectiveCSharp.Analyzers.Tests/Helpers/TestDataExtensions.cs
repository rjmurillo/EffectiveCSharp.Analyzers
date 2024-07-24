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
                foreach (var g in ReferenceAssemblyCatalog.Catalog.Keys)
                {
                    retVal.Add(g, (string)f);
                }
            }
        }

        return retVal;
    }
}

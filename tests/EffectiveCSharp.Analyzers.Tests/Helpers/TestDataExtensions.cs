namespace EffectiveCSharp.Analyzers.Tests.Helpers;

internal static class TestDataExtensions
{
    public static TheoryData<string, string> WithReferenceAssemblyGroups(this TheoryData<string> data)
    {
        TheoryData<string, string> retVal = new();

        foreach (object[]? theoryDataItem in data)
        {
            foreach (object entry in theoryDataItem)
            {
                foreach (string referenceAssembly in ReferenceAssemblyCatalog.Catalog.Keys)
                {
                    retVal.Add(referenceAssembly, (string)entry);
                }
            }
        }

        return retVal;
    }
}

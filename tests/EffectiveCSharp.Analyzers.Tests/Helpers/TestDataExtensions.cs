namespace EffectiveCSharp.Analyzers.Tests.Helpers;

internal static class TestDataExtensions
{
    internal static TheoryData<string, string> WithReferenceAssemblyGroups(this TheoryData<string> data, Predicate<string>? predicate = null)
    {
        TheoryData<string, string> retVal = [];
        predicate ??= _ => true;

        foreach (object[]? theoryDataItem in data)
        {
            foreach (object entry in theoryDataItem)
            {
                foreach (string referenceAssembly in ReferenceAssemblyCatalog.Catalog.Keys)
                {
                    if (predicate(referenceAssembly))
                    {
                        retVal.Add(referenceAssembly, (string)entry);
                    }
                }
            }
        }

        return retVal;
    }
}

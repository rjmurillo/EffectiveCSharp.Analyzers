namespace EffectiveCSharp.Analyzers.Tests.Helpers;

internal static class TestDataExtensions
{
    public static IEnumerable<object[]> WithReferenceAssemblyGroups(this IEnumerable<object[]> data)
    {
        foreach (object[] item in data)
        {
            yield return item.Prepend(ReferenceAssemblyCatalog.Net80).ToArray();
        }
    }
}

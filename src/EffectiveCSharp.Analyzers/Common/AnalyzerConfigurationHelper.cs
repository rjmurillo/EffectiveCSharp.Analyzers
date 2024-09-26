namespace EffectiveCSharp.Analyzers.Common;

internal static class AnalyzerConfigurationHelper
{
    internal static List<string> GetConfiguredSafeItems(AnalyzerOptions options, string diagnosticId = DiagnosticIds.StaticClassMemberInitialization)
    {
        List<string> safeItems = [];
        AnalyzerConfigOptions configOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;

        if (!configOptions.TryGetValue($"dotnet_diagnostic.{diagnosticId}.safe_items", out string? items))
        {
            return safeItems;
        }

        ReadOnlySpan<char> itemsSpan = items.AsSpan();
        int start = 0;

        while (start < itemsSpan.Length)
        {
            // Skip leading whitespace
            while (start < itemsSpan.Length && char.IsWhiteSpace(itemsSpan[start]))
            {
                start++;
            }

            if (start >= itemsSpan.Length)
            {
                break;
            }

            // Find the end of the current item
            int end = start;
            while (end < itemsSpan.Length && itemsSpan[end] != ',')
            {
                end++;
            }

            // Trim trailing whitespace
            int itemEnd = end;
            while (itemEnd > start && char.IsWhiteSpace(itemsSpan[itemEnd - 1]))
            {
                itemEnd--;
            }

            if (itemEnd > start)
            {
                // Add the trimmed item to the list
                safeItems.Add(itemsSpan.Slice(start, itemEnd - start).ToString());
            }

            start = end + 1;
        }

        return safeItems;
    }
}

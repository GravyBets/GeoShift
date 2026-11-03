using System.Text;

namespace GeoShift.Core.Services;

/// <summary>Conservative header suggestions. Never applies mappings automatically.</summary>
public static class ColumnNameSuggester
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["radioserialnumber"] = "radiosn",
        ["radioserial"] = "radiosn",
        ["radiosn"] = "radiosn",
        ["cyberlockserialnumber"] = "cyberlocksn",
        ["cyberlockserial"] = "cyberlocksn",
        ["cyberlocksn"] = "cyberlocksn",
        ["lockserialnumber"] = "cyberlocksn",
        ["lockserial"] = "cyberlocksn",
        ["sitenumber"] = "site",
        ["siteid"] = "site",
        ["site"] = "site"
    };

    public static IReadOnlyList<ColumnNameSuggestion> Suggest(
        IReadOnlyList<ColumnNameDataset> datasets)
    {
        ArgumentNullException.ThrowIfNull(datasets);
        if (datasets.Count < 2) return [];
        var groups = datasets.SelectMany((dataset, datasetIndex) =>
                dataset.Columns.Where(column => !string.Equals(column, dataset.IdentifierColumn, StringComparison.OrdinalIgnoreCase))
                    .Select(column => new Candidate(datasetIndex, column, Canonical(column))))
            .Where(candidate => candidate.Key.Length > 0)
            .GroupBy(candidate => candidate.Key, StringComparer.OrdinalIgnoreCase);

        var suggestions = new List<ColumnNameSuggestion>();
        foreach (var group in groups)
        {
            var members = group.ToArray();
            if (members.Select(x => x.DatasetIndex).Distinct().Count() < 2) continue;
            // Ambiguous aliases within a file are not safe to auto-suggest.
            if (members.GroupBy(x => x.DatasetIndex).Any(x => x.Count() > 1)) continue;
            var target = members.OrderBy(x => x.DatasetIndex).First().Column;
            foreach (var member in members)
            {
                if (!string.Equals(member.Column, target, StringComparison.Ordinal))
                    suggestions.Add(new ColumnNameSuggestion(member.DatasetIndex, member.Column, target));
            }
        }
        return suggestions;
    }

    private static string Canonical(string value)
    {
        var normalized = new string(value.Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant).ToArray());
        return Aliases.TryGetValue(normalized, out var alias) ? alias : normalized;
    }

    private sealed record Candidate(int DatasetIndex, string Column, string Key);
}

public sealed record ColumnNameDataset(IReadOnlyList<string> Columns, string IdentifierColumn);
public sealed record ColumnNameSuggestion(int DatasetIndex, string SourceColumn, string SuggestedOutputColumn);

namespace GeoShift.Core.Services;

/// <summary>
/// Explicit, non-destructive column mapping for migration workflows.
/// Source values are never changed and conflicting target assignments are rejected.
/// </summary>
public sealed class ColumnMappingEngine
{
    public ColumnMappingResult Map(
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyCollection<ColumnMappingRule> rules,
        bool includeUnmappedColumns = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rules);

        var output = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var changes = new List<ColumnMappingChange>();
        var sourceLookup = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            if (!sourceLookup.TryAdd(item.Key, item.Value))
                throw new ArgumentException("Source contains duplicate column names differing only by case.", nameof(source));
        }

        var targetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.SourceColumn) ||
                string.IsNullOrWhiteSpace(rule.TargetColumn))
                throw new ArgumentException("Mapping rules require nonempty source and target column names.", nameof(rules));

            if (!sourceLookup.TryGetValue(rule.SourceColumn, out var value))
                throw new InvalidDataException($"Source column '{rule.SourceColumn}' was not found.");

            if (!targetNames.Add(rule.TargetColumn))
                throw new InvalidDataException($"Multiple columns map to target '{rule.TargetColumn}'.");

            if (!mappedSources.Add(rule.SourceColumn))
                throw new InvalidDataException($"Source column '{rule.SourceColumn}' is mapped more than once.");

            output.Add(rule.TargetColumn, value);
            changes.Add(new ColumnMappingChange(rule.SourceColumn, rule.TargetColumn, value));
        }

        if (includeUnmappedColumns)
        {
            foreach (var item in sourceLookup)
            {
                if (mappedSources.Contains(item.Key)) continue;
                if (output.ContainsKey(item.Key))
                    throw new InvalidDataException(
                        $"Unmapped source column '{item.Key}' conflicts with a mapped target. " +
                        "Rename or explicitly map the conflicting column.");
                output.Add(item.Key, item.Value);
            }
        }

        return new ColumnMappingResult(output, changes);
    }
}

public sealed record ColumnMappingRule(string SourceColumn, string TargetColumn);

public sealed record ColumnMappingChange(string SourceColumn, string TargetColumn, string? OriginalValue);

public sealed record ColumnMappingResult(
    IReadOnlyDictionary<string, string?> Columns,
    IReadOnlyList<ColumnMappingChange> Changes);

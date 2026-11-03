namespace GeoShift.Core.Services;

/// <summary>
/// Reports duplicate identifier groups without changing or removing source records.
/// Comparison uses explicit, caller-selected columns and ordinal string semantics.
/// </summary>
public sealed class DuplicateDetectionEngine
{
    public DuplicateDetectionResult Detect(
        DatasetMergeResult dataset,
        IReadOnlyList<string> keyColumns,
        bool ignoreCase = true,
        bool trimWhitespace = true)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(keyColumns);
        if (keyColumns.Count == 0)
            throw new ArgumentException("Select at least one key column.", nameof(keyColumns));

        var available = new HashSet<string>(dataset.Columns, StringComparer.OrdinalIgnoreCase);
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in keyColumns)
        {
            if (string.IsNullOrWhiteSpace(column) || !available.Contains(column))
                throw new ArgumentException($"Unknown key column '{column}'.", nameof(keyColumns));
            if (!selected.Add(column))
                throw new ArgumentException($"Repeated key column '{column}'.", nameof(keyColumns));
        }

        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var groups = new Dictionary<string[], List<int>>(new CompositeKeyComparer(comparer));
        var skipped = new List<int>();

        for (var i = 0; i < dataset.Rows.Count; i++)
        {
            var row = dataset.Rows[i];
            var key = new string[keyColumns.Count];
            var complete = true;
            for (var j = 0; j < keyColumns.Count; j++)
            {
                if (!row.Columns.TryGetValue(keyColumns[j], out var value))
                {
                    value = row.Columns.FirstOrDefault(
                        x => string.Equals(x.Key, keyColumns[j], StringComparison.OrdinalIgnoreCase)).Value;
                }
                if (trimWhitespace) value = value?.Trim();
                if (string.IsNullOrEmpty(value)) { complete = false; break; }
                key[j] = value;
            }
            if (!complete) { skipped.Add(i); continue; }
            if (!groups.TryGetValue(key, out var indices))
                groups.Add(key, indices = new List<int>());
            indices.Add(i);
        }

        var duplicates = new List<DuplicateGroup>();
        foreach (var (key, indices) in groups)
        {
            if (indices.Count < 2) continue;
            var conflictColumns = new List<string>();
            foreach (var column in dataset.Columns)
            {
                if (selected.Contains(column)) continue;
                var values = new HashSet<string?>(comparer);
                foreach (var index in indices)
                {
                    var row = dataset.Rows[index];
                    row.Columns.TryGetValue(column, out var value);
                    if (trimWhitespace) value = value?.Trim();
                    values.Add(value);
                }
                if (values.Count > 1) conflictColumns.Add(column);
            }
            duplicates.Add(new DuplicateGroup(
                keyColumns.Zip(key, (name, value) => new KeyValuePair<string, string>(name, value))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                indices.ToArray(),
                conflictColumns));
        }

        return new DuplicateDetectionResult(duplicates, skipped);
    }

    private sealed class CompositeKeyComparer(StringComparer comparer) : IEqualityComparer<string[]>
    {
        public bool Equals(string[]? x, string[]? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null || x.Length != y.Length) return false;
            for (var i = 0; i < x.Length; i++)
                if (!comparer.Equals(x[i], y[i])) return false;
            return true;
        }

        public int GetHashCode(string[] obj)
        {
            var hash = new HashCode();
            foreach (var value in obj) hash.Add(value, comparer);
            return hash.ToHashCode();
        }
    }
}

public sealed record DuplicateGroup(
    IReadOnlyDictionary<string, string> Key,
    IReadOnlyList<int> RowIndices,
    IReadOnlyList<string> ConflictingColumns)
{
    public bool HasConflicts => ConflictingColumns.Count > 0;
}

public sealed record DuplicateDetectionResult(
    IReadOnlyList<DuplicateGroup> Groups,
    IReadOnlyList<int> SkippedRowIndices);

namespace GeoShift.Core.Services;

/// <summary>
/// Applies explicit, per-group conflict decisions to a merged dataset.
/// No records are removed unless the caller provides a decision for that group.
/// Source rows remain unchanged and all decisions are recorded.
/// </summary>
public sealed class ConflictResolutionEngine
{
    public ConflictResolutionResult Resolve(
        DatasetMergeResult dataset,
        DuplicateDetectionResult detection,
        IReadOnlyCollection<ConflictResolutionDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(decisions);

        var groups = detection.Groups;
        var groupByIndex = new Dictionary<int, DuplicateGroup>();
        foreach (var group in groups)
        {
            if (group.RowIndices.Count < 2 || group.RowIndices.Distinct().Count() != group.RowIndices.Count)
                throw new InvalidDataException("Duplicate group must contain distinct row indices.");
            foreach (var index in group.RowIndices)
            {
                if (index < 0 || index >= dataset.Rows.Count || !groupByIndex.TryAdd(index, group))
                    throw new InvalidDataException("Duplicate groups contain invalid or overlapping row indices.");
            }
        }

        var decisionsByGroup = new Dictionary<DuplicateGroup, ConflictResolutionDecision>();
        foreach (var decision in decisions)
        {
            if (decision is null)
                throw new ArgumentException("Decisions cannot contain null entries.", nameof(decisions));
            if (!groupByIndex.TryGetValue(decision.KeepRowIndex, out var group))
                throw new InvalidDataException($"Row {decision.KeepRowIndex} does not belong to a duplicate group.");
            if (!decisionsByGroup.TryAdd(group, decision))
                throw new InvalidDataException("Multiple decisions were provided for the same duplicate group.");
            if (decision.ColumnSources is null)
                throw new InvalidDataException("Column sources cannot be null.");
            foreach (var (column, index) in decision.ColumnSources)
            {
                if (!dataset.Columns.Contains(column, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Unknown column '{column}'.");
                if (!group.RowIndices.Contains(index))
                    throw new InvalidDataException($"Source row {index} is outside the duplicate group.");
            }
        }

        var output = new List<ResolvedDatasetRow>();
        var audits = new List<ConflictResolutionAudit>();
        var handled = new HashSet<int>();
        for (var index = 0; index < dataset.Rows.Count; index++)
        {
            if (handled.Contains(index)) continue;
            if (!groupByIndex.TryGetValue(index, out var group) ||
                !decisionsByGroup.TryGetValue(group, out var decision))
            {
                var original = dataset.Rows[index];
                output.Add(new ResolvedDatasetRow(index, original.SourceDataset,
                    original.SourceRowNumber,
                    original.Columns.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)));
                continue;
            }

            foreach (var member in group.RowIndices) handled.Add(member);
            var kept = dataset.Rows[decision.KeepRowIndex];
            var values = kept.Columns.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            var selectedSources = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (column, sourceIndex) in decision.ColumnSources)
            {
                var source = dataset.Rows[sourceIndex];
                source.Columns.TryGetValue(column, out var value);
                values[column] = value;
                selectedSources.Add(column, sourceIndex);
            }

            output.Add(new ResolvedDatasetRow(decision.KeepRowIndex, kept.SourceDataset,
                kept.SourceRowNumber, values));
            audits.Add(new ConflictResolutionAudit(
                decision.KeepRowIndex,
                group.RowIndices.Where(i => i != decision.KeepRowIndex).ToArray(),
                selectedSources,
                group.ConflictingColumns.ToArray()));
        }

        return new ConflictResolutionResult(output, audits);
    }
}

/// <param name="KeepRowIndex">Zero-based merged dataset row to retain.</param>
/// <param name="ColumnSources">Optional column-to-row selections; unspecified columns come from the kept row.</param>
public sealed record ConflictResolutionDecision(
    int KeepRowIndex,
    IReadOnlyDictionary<string, int> ColumnSources);

public sealed record ResolvedDatasetRow(
    int OriginalRowIndex,
    string SourceDataset,
    int SourceRowNumber,
    IReadOnlyDictionary<string, string?> Columns);

public sealed record ConflictResolutionAudit(
    int KeptRowIndex,
    IReadOnlyList<int> RemovedRowIndices,
    IReadOnlyDictionary<string, int> ColumnSources,
    IReadOnlyList<string> ConflictingColumns);

public sealed record ConflictResolutionResult(
    IReadOnlyList<ResolvedDatasetRow> Rows,
    IReadOnlyList<ConflictResolutionAudit> Audit);

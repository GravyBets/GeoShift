namespace GeoShift.Core.Services;

/// <summary>
/// Read-only preview of migration outcomes. Counts describe input rows, proposed output,
/// unresolved duplicate groups, and the explicit decisions applied.
/// </summary>
public sealed class MigrationReportEngine
{
    public MigrationReport Build(
        DatasetMergeResult input,
        DuplicateDetectionResult duplicates,
        ConflictResolutionResult resolved)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(duplicates);
        ArgumentNullException.ThrowIfNull(resolved);

        var total = input.Rows.Count;
        var duplicateIndices = new HashSet<int>();
        foreach (var group in duplicates.Groups)
        {
            if (group.RowIndices.Count < 2)
                throw new InvalidDataException("Duplicate group must have at least two rows.");
            foreach (var index in group.RowIndices)
                if (index < 0 || index >= total || !duplicateIndices.Add(index))
                    throw new InvalidDataException("Duplicate groups contain invalid or overlapping indices.");
        }

        var skipped = new HashSet<int>();
        foreach (var index in duplicates.SkippedRowIndices)
            if (index < 0 || index >= total || duplicateIndices.Contains(index) || !skipped.Add(index))
                throw new InvalidDataException("Invalid or repeated skipped row index.");

        var removed = new HashSet<int>();
        var kept = new HashSet<int>();
        foreach (var audit in resolved.Audit)
        {
            if (audit.KeptRowIndex < 0 || audit.KeptRowIndex >= total ||
                !duplicateIndices.Contains(audit.KeptRowIndex) ||
                !kept.Add(audit.KeptRowIndex))
                throw new InvalidDataException("Audit references an invalid or repeated kept row.");
            foreach (var index in audit.RemovedRowIndices)
                if (index < 0 || index >= total || !duplicateIndices.Contains(index) ||
                    index == audit.KeptRowIndex || !removed.Add(index))
                    throw new InvalidDataException("Audit references an invalid or repeated removed row.");
        }

        var expectedIndices = Enumerable.Range(0, total).Where(i => !removed.Contains(i)).ToHashSet();
        var actualIndices = new HashSet<int>();
        foreach (var row in resolved.Rows)
            if (!expectedIndices.Contains(row.OriginalRowIndex) || !actualIndices.Add(row.OriginalRowIndex))
                throw new InvalidDataException("Resolved rows contain an unexpected or repeated source index.");
        if (!actualIndices.SetEquals(expectedIndices))
            throw new InvalidDataException("Resolved rows do not match the audited decisions.");

        var resolvedGroups = 0;
        foreach (var group in duplicates.Groups)
        {
            var groupKept = group.RowIndices.Where(kept.Contains).ToArray();
            if (groupKept.Length == 0)
            {
                if (group.RowIndices.Any(removed.Contains))
                    throw new InvalidDataException("An unresolved duplicate group has removed rows.");
                continue;
            }
            if (groupKept.Length != 1 || group.RowIndices.Count - 1 !=
                group.RowIndices.Count(removed.Contains))
                throw new InvalidDataException("Audit does not fully resolve a duplicate group.");
            resolvedGroups++;
        }

        var sources = input.Rows
            .GroupBy(x => x.SourceDataset, StringComparer.OrdinalIgnoreCase)
            .Select(g => new MigrationSourceSummary(g.Key, g.Count()))
            .ToArray();

        return new MigrationReport(
            total,
            resolved.Rows.Count,
            duplicates.Groups.Count,
            resolvedGroups,
            duplicates.Groups.Count - resolvedGroups,
            duplicates.Groups.Count(g => g.HasConflicts),
            skipped.Count,
            removed.Count,
            sources,
            resolved.Audit.ToArray());
    }
}

public sealed record MigrationSourceSummary(string SourceDataset, int ImportedRows);

public sealed record MigrationReport(
    int ImportedRows,
    int OutputRows,
    int DuplicateGroups,
    int ResolvedGroups,
    int UnresolvedGroups,
    int GroupsWithConflicts,
    int RowsMissingMatchKeys,
    int RowsConsolidated,
    IReadOnlyList<MigrationSourceSummary> Sources,
    IReadOnlyList<ConflictResolutionAudit> Decisions);

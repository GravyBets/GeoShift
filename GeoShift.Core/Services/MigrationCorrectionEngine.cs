namespace GeoShift.Core.Services;

/// <summary>Applies explicitly approved edits to export rows, preserving the resolved input.</summary>
public static class MigrationCorrectionEngine
{
    public static MigrationCorrectionResult Apply(
        ConflictResolutionResult resolved,
        IReadOnlyList<string> columns,
        IReadOnlyCollection<MigrationCorrection> corrections)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(corrections);
        var allowed = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
        var byIndex = resolved.Rows.ToDictionary(row => row.OriginalRowIndex);
        var changes = new Dictionary<(int, string), MigrationCorrection>();
        foreach (var correction in corrections)
        {
            if (correction is null || string.IsNullOrWhiteSpace(correction.Column) ||
                !allowed.Contains(correction.Column) || !byIndex.TryGetValue(correction.OriginalRowIndex, out var row))
                throw new InvalidDataException("Correction refers to an unknown output row or column.");
            row.Columns.TryGetValue(correction.Column, out var current);
            if (!string.Equals(current, correction.OriginalValue, StringComparison.Ordinal))
                throw new InvalidDataException("Correction no longer matches the original value. Review it again.");
            if (!changes.TryAdd((correction.OriginalRowIndex, correction.Column.ToUpperInvariant()), correction))
                throw new InvalidDataException("A cell cannot have multiple corrections.");
        }
        var output = resolved.Rows.Select(row =>
        {
            var values = row.Columns.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var change in corrections.Where(x => x.OriginalRowIndex == row.OriginalRowIndex))
                values[change.Column] = change.CorrectedValue;
            return new ResolvedDatasetRow(row.OriginalRowIndex, row.SourceDataset, row.SourceRowNumber, values);
        }).ToArray();
        return new MigrationCorrectionResult(new ConflictResolutionResult(output, resolved.Audit), corrections.ToArray());
    }
}

public sealed record MigrationCorrection(int OriginalRowIndex, string Column, string? OriginalValue, string? CorrectedValue);
public sealed record MigrationCorrectionResult(ConflictResolutionResult Resolved, IReadOnlyList<MigrationCorrection> Audit);

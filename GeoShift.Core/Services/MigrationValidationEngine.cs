namespace GeoShift.Core.Services;

/// <summary>Non-destructive checks on the rows that would be exported.</summary>
public static class MigrationValidationEngine
{
    public static MigrationValidationResult Validate(
        ConflictResolutionResult resolved,
        IReadOnlyList<string> columns,
        IReadOnlyCollection<string> requiredColumns)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(requiredColumns);
        var available = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in requiredColumns)
        {
            if (string.IsNullOrWhiteSpace(column) || !available.Contains(column))
                throw new ArgumentException($"Unknown required column '{column}'.", nameof(requiredColumns));
            required.Add(column);
        }
        var issues = new List<MigrationValidationIssue>();
        foreach (var row in resolved.Rows)
        {
            foreach (var column in columns)
            {
                row.Columns.TryGetValue(column, out var value);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (required.Contains(column))
                        issues.Add(new MigrationValidationIssue(row.OriginalRowIndex, row.SourceDataset,
                            row.SourceRowNumber, column, ValidationSeverity.Error, "Required value is missing"));
                    continue;
                }
                if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                    issues.Add(new MigrationValidationIssue(row.OriginalRowIndex, row.SourceDataset,
                        row.SourceRowNumber, column, ValidationSeverity.Warning, "Leading or trailing whitespace"));
                if (value.Any(char.IsControl))
                    issues.Add(new MigrationValidationIssue(row.OriginalRowIndex, row.SourceDataset,
                        row.SourceRowNumber, column, ValidationSeverity.Warning, "Contains control characters"));
            }
        }
        return new MigrationValidationResult(issues);
    }
}

public enum ValidationSeverity { Warning, Error }
public sealed record MigrationValidationIssue(
    int OriginalRowIndex, string SourceDataset, int SourceRowNumber,
    string Column, ValidationSeverity Severity, string Message);
public sealed record MigrationValidationResult(IReadOnlyList<MigrationValidationIssue> Issues)
{
    public int ErrorCount => Issues.Count(issue => issue.Severity == ValidationSeverity.Error);
    public int WarningCount => Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
}

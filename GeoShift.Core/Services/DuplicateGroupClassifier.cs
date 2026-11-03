namespace GeoShift.Core.Services;

/// <summary>Classifies duplicate groups without changing their source rows or decisions.</summary>
public static class DuplicateGroupClassifier
{
    public static DuplicateGroupClassification Classify(
        DatasetMergeResult merged, DuplicateGroup group)
    {
        ArgumentNullException.ThrowIfNull(merged);
        ArgumentNullException.ThrowIfNull(group);
        var missing = new List<string>();
        var disagreements = new List<string>();
        foreach (var column in group.ConflictingColumns)
        {
            var values = group.RowIndices.Select(index =>
                merged.Rows[index].Columns.TryGetValue(column, out var value) ? value?.Trim() : null).ToArray();
            var nonempty = values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (nonempty.Length > 1) disagreements.Add(column);
            else if (nonempty.Length == 1 && values.Any(string.IsNullOrWhiteSpace)) missing.Add(column);
        }
        var kind = disagreements.Count > 0 ? DuplicateGroupKind.Conflicting :
            missing.Count > 0 ? DuplicateGroupKind.Complementary : DuplicateGroupKind.Identical;
        return new DuplicateGroupClassification(kind, missing, disagreements);
    }
}

public enum DuplicateGroupKind { Identical, Complementary, Conflicting }

public sealed record DuplicateGroupClassification(
    DuplicateGroupKind Kind,
    IReadOnlyList<string> MissingInformationColumns,
    IReadOnlyList<string> DisagreementColumns);

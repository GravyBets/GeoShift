namespace GeoShift.Core.Services;

/// <summary>
/// Combines datasets without deduplicating or silently discarding rows.
/// Every output row records its original dataset and one-based row number.
/// </summary>
public sealed class DatasetMergeEngine
{
    public DatasetMergeResult Merge(
        IReadOnlyCollection<MergeDataset> datasets,
        IReadOnlyCollection<string>? preferredColumnOrder = null)
    {
        ArgumentNullException.ThrowIfNull(datasets);
        var columns = new List<string>();
        var seenColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDatasets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (preferredColumnOrder is not null)
        {
            foreach (var column in preferredColumnOrder)
            {
                ValidateColumn(column);
                if (!seenColumns.Add(column))
                    throw new InvalidDataException($"Duplicate preferred column '{column}'.");
                columns.Add(column);
            }
        }

        foreach (var dataset in datasets)
        {
            if (dataset is null || string.IsNullOrWhiteSpace(dataset.Name) || dataset.Rows is null)
                throw new ArgumentException("Each dataset requires a name and rows.", nameof(datasets));
            if (!seenDatasets.Add(dataset.Name))
                throw new InvalidDataException($"Duplicate dataset name '{dataset.Name}'.");
            foreach (var row in dataset.Rows)
            {
                if (row is null)
                    throw new InvalidDataException($"Dataset '{dataset.Name}' contains a null row.");
                var rowNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in row.Keys)
                {
                    ValidateColumn(key);
                    if (!rowNames.Add(key))
                        throw new InvalidDataException($"Row in '{dataset.Name}' contains case-insensitive duplicate column '{key}'.");
                    if (seenColumns.Add(key)) columns.Add(key);
                }
            }
        }

        var merged = new List<MergedDatasetRow>();
        foreach (var dataset in datasets)
        {
            var rowNumber = 0;
            foreach (var row in dataset.Rows)
            {
                rowNumber++;
                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in columns) values[column] = null;
                foreach (var item in row) values[item.Key] = item.Value;
                merged.Add(new MergedDatasetRow(dataset.Name, rowNumber, values));
            }
        }

        return new DatasetMergeResult(columns, merged);
    }

    private static void ValidateColumn(string? column)
    {
        if (string.IsNullOrWhiteSpace(column))
            throw new InvalidDataException("Column names cannot be empty.");
    }
}

public sealed record MergeDataset(
    string Name,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

public sealed record MergedDatasetRow(
    string SourceDataset,
    int SourceRowNumber,
    IReadOnlyDictionary<string, string?> Columns);

public sealed record DatasetMergeResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<MergedDatasetRow> Rows);

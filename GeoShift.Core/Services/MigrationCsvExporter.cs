using System.Text;
namespace GeoShift.Core.Services;
public static class MigrationCsvExporter
{
    public static string Cleaned(DatasetMergeResult merged, ConflictResolutionResult resolved)
    {
        using var output = new StringWriter();
        Write(output, merged.Columns);
        foreach (var row in resolved.Rows)
            Write(output, merged.Columns.Select(c => row.Columns.TryGetValue(c, out var v) ? v : null));
        return output.ToString();
    }

    public static string Audit(DatasetMergeResult merged, DuplicateDetectionResult duplicates,
        ConflictResolutionResult resolved, IReadOnlyCollection<int> keptSeparate)
    {
        using var output = new StringWriter();
        Write(output, new[] {"Decision","Identifier","Source file","Source row","Selected file","Selected row","Field selections"});
        var byIndex = new Dictionary<int, ConflictResolutionAudit>();
        foreach (var audit in resolved.Audit)
        {
            byIndex[audit.KeptRowIndex] = audit;
            foreach (var index in audit.RemovedRowIndices) byIndex[index] = audit;
        }
        foreach (var group in duplicates.Groups)
        {
            var first = group.RowIndices[0];
            byIndex.TryGetValue(first, out var audit);
            var decision = audit != null ? "Combined" : keptSeparate.Contains(first) ? "Kept separate" : "Unreviewed";
            var selected = audit == null ? null : merged.Rows[audit.KeptRowIndex];
            var choices = audit == null ? "" : string.Join("; ", audit.ColumnSources.Select(choice =>
                choice.Key + " from " + merged.Rows[choice.Value].SourceDataset +
                " row " + merged.Rows[choice.Value].SourceRowNumber));
            foreach (var index in group.RowIndices)
            {
                var source = merged.Rows[index];
                Write(output, new[] {decision, string.Join(" | ", group.Key.Values), source.SourceDataset,
                    source.SourceRowNumber.ToString(), selected?.SourceDataset,
                    selected?.SourceRowNumber.ToString(), choices});
            }
        }
        foreach (var index in duplicates.SkippedRowIndices)
        {
            var source = merged.Rows[index];
            Write(output, new[] {"Missing identifier","",source.SourceDataset,source.SourceRowNumber.ToString(),"","",""});
        }
        return output.ToString();
    }

    private static void Write(TextWriter output, IEnumerable<string?> values) =>
        output.WriteLine(string.Join(",", values.Select(Escape)));

    private static string Escape(string? value)
    {
        var s = value ?? "";
        var trimmed = s.TrimStart();
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}

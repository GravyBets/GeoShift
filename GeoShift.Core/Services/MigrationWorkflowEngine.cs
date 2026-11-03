namespace GeoShift.Core.Services;

/// <summary>
/// Coordinates mapping, merging, duplicate detection, explicit resolution and reporting.
/// All inputs are retained; no decision means no records are consolidated.
/// </summary>
public sealed class MigrationWorkflowEngine
{
    private readonly ColumnMappingEngine _mapper = new();
    private readonly DatasetMergeEngine _merger = new();
    private readonly DuplicateDetectionEngine _detector = new();
    private readonly ConflictResolutionEngine _resolver = new();
    private readonly MigrationReportEngine _reporter = new();

    public MigrationWorkflowResult Execute(MigrationWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Datasets);
        ArgumentNullException.ThrowIfNull(request.MatchColumns);
        ArgumentNullException.ThrowIfNull(request.Decisions);

        var mappedDatasets = new List<MergeDataset>();
        var mappingAudit = new List<MigrationMappingAudit>();
        foreach (var dataset in request.Datasets)
        {
            if (dataset is null || dataset.Rows is null || dataset.Mappings is null)
                throw new ArgumentException("Each dataset needs rows and mappings.", nameof(request));

            var mappedRows = new List<IReadOnlyDictionary<string, string?>>();
            var rowIndex = 0;
            foreach (var row in dataset.Rows)
            {
                if (row is null)
                    throw new InvalidDataException($"Dataset '{dataset.Name}' has a null row.");
                var mapped = _mapper.Map(row, dataset.Mappings, request.IncludeUnmappedColumns);
                mappedRows.Add(mapped.Columns);
                rowIndex++;
                foreach (var change in mapped.Changes)
                    mappingAudit.Add(new MigrationMappingAudit(
                        dataset.Name, rowIndex, change.SourceColumn, change.TargetColumn));
            }
            mappedDatasets.Add(new MergeDataset(dataset.Name, mappedRows));
        }

        var merged = _merger.Merge(mappedDatasets, request.PreferredColumnOrder);
        var duplicates = _detector.Detect(
            merged, request.MatchColumns, request.IgnoreKeyCase, request.TrimKeyWhitespace);
        var resolved = _resolver.Resolve(merged, duplicates, request.Decisions);
        var report = _reporter.Build(merged, duplicates, resolved);
        return new MigrationWorkflowResult(merged, duplicates, resolved, report, mappingAudit);
    }
}

public sealed record MigrationWorkflowDataset(
    string Name,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    IReadOnlyCollection<ColumnMappingRule> Mappings);

public sealed record MigrationWorkflowRequest(
    IReadOnlyCollection<MigrationWorkflowDataset> Datasets,
    IReadOnlyList<string> MatchColumns,
    IReadOnlyCollection<ConflictResolutionDecision> Decisions,
    bool IncludeUnmappedColumns = true,
    bool IgnoreKeyCase = true,
    bool TrimKeyWhitespace = true,
    IReadOnlyCollection<string>? PreferredColumnOrder = null);

public sealed record MigrationMappingAudit(
    string SourceDataset,
    int SourceRowNumber,
    string SourceColumn,
    string TargetColumn);

public sealed record MigrationWorkflowResult(
    DatasetMergeResult Merged,
    DuplicateDetectionResult Duplicates,
    ConflictResolutionResult Resolved,
    MigrationReport Report,
    IReadOnlyList<MigrationMappingAudit> MappingAudit);

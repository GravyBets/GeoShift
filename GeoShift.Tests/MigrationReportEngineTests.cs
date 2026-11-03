using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class MigrationReportEngineTests
{
    private static (DatasetMergeResult Data, DuplicateDetectionResult Duplicates) Setup()
    {
        var data = new DatasetMergeEngine().Merge(
        [
            new MergeDataset("a.csv",
            [
                new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "A" },
                new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "B" }
            ]),
            new MergeDataset("b.csv",
            [
                new Dictionary<string, string?> { ["Site"] = "102", ["Radio"] = "C" },
                new Dictionary<string, string?> { ["Site"] = "", ["Radio"] = "D" }
            ])
        ]);
        return (data, new DuplicateDetectionEngine().Detect(data, ["Site"]));
    }

    [Fact]
    public void Build_ReportsUnresolvedDuplicatesWithoutDroppingRows()
    {
        var (data, duplicates) = Setup();
        var resolved = new ConflictResolutionEngine().Resolve(data, duplicates, []);
        var report = new MigrationReportEngine().Build(data, duplicates, resolved);

        Assert.Equal(4, report.ImportedRows);
        Assert.Equal(4, report.OutputRows);
        Assert.Equal(1, report.DuplicateGroups);
        Assert.Equal(1, report.UnresolvedGroups);
        Assert.Equal(1, report.GroupsWithConflicts);
        Assert.Equal(1, report.RowsMissingMatchKeys);
        Assert.Equal(0, report.RowsConsolidated);
        Assert.Equal(2, report.Sources.Count);
        Assert.Equal(2, report.Sources[0].ImportedRows);
    }

    [Fact]
    public void Build_ReportsExplicitlyConsolidatedRowsAndDecisions()
    {
        var (data, duplicates) = Setup();
        var resolved = new ConflictResolutionEngine().Resolve(data, duplicates,
        [
            new ConflictResolutionDecision(0, new Dictionary<string, int> { ["Radio"] = 1 })
        ]);
        var report = new MigrationReportEngine().Build(data, duplicates, resolved);

        Assert.Equal(3, report.OutputRows);
        Assert.Equal(1, report.ResolvedGroups);
        Assert.Equal(0, report.UnresolvedGroups);
        Assert.Equal(1, report.RowsConsolidated);
        Assert.Single(report.Decisions);
        Assert.Equal("A", data.Rows[0].Columns["Radio"]);
    }

    [Fact]
    public void Build_RejectsInconsistentOutput()
    {
        var (data, duplicates) = Setup();
        var resolved = new ConflictResolutionEngine().Resolve(data, duplicates, []);
        var invalid = resolved with { Rows = resolved.Rows.Skip(1).ToArray() };
        Assert.Throws<InvalidDataException>(() =>
            new MigrationReportEngine().Build(data, duplicates, invalid));
    }

    [Fact]
    public void Build_HandlesEmptyDataset()
    {
        var data = new DatasetMergeEngine().Merge([]);
        var duplicates = new DuplicateDetectionResult([], []);
        var resolved = new ConflictResolutionEngine().Resolve(data, duplicates, []);
        var report = new MigrationReportEngine().Build(data, duplicates, resolved);
        Assert.Equal(0, report.ImportedRows);
        Assert.Equal(0, report.OutputRows);
    }
}

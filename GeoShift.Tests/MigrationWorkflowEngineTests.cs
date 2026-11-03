using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class MigrationWorkflowEngineTests
{
    private static MigrationWorkflowRequest Request(IReadOnlyCollection<ConflictResolutionDecision>? decisions = null) =>
        new(
        [
            new MigrationWorkflowDataset("radio.csv",
            [
                new Dictionary<string, string?> { ["Site #"] = "101", ["Radio SN"] = "ABC" },
                new Dictionary<string, string?> { ["Site #"] = "102", ["Radio SN"] = "DEF" }
            ],
            [
                new ColumnMappingRule("Site #", "Site ID"),
                new ColumnMappingRule("Radio SN", "Serial Number")
            ]),
            new MigrationWorkflowDataset("location.csv",
            [
                new Dictionary<string, string?> { ["Location"] = "101", ["Latitude"] = "29.9" }
            ],
            [
                new ColumnMappingRule("Location", "Site ID")
            ])
        ],
        ["Site ID"],
        decisions ?? []);

    [Fact]
    public void Execute_MapsAndMergesAndReportsWithoutDiscardingRows()
    {
        var result = new MigrationWorkflowEngine().Execute(Request());
        Assert.Equal(3, result.Merged.Rows.Count);
        Assert.Equal("101", result.Merged.Rows[0].Columns["Site ID"]);
        Assert.Equal("101", result.Merged.Rows[2].Columns["Site ID"]);
        Assert.Equal(3, result.Resolved.Rows.Count);
        Assert.Equal(1, result.Report.DuplicateGroups);
        Assert.Equal(1, result.Report.UnresolvedGroups);
        Assert.Equal(3, result.MappingAudit.Count);
        Assert.Equal("location.csv", result.Merged.Rows[2].SourceDataset);
    }

    [Fact]
    public void Execute_AppliesExplicitFieldSelectionAndCreatesReport()
    {
        var result = new MigrationWorkflowEngine().Execute(Request(
        [
            new ConflictResolutionDecision(0, new Dictionary<string, int> { ["Latitude"] = 2 })
        ]));
        Assert.Equal(2, result.Resolved.Rows.Count);
        Assert.Equal("29.9", result.Resolved.Rows[0].Columns["Latitude"]);
        Assert.Equal(1, result.Report.RowsConsolidated);
        Assert.Equal(0, result.Report.UnresolvedGroups);
        Assert.Single(result.Report.Decisions);
    }

    [Fact]
    public void Execute_RejectsMissingMatchColumn()
    {
        var request = Request() with { MatchColumns = ["DoesNotExist"] };
        Assert.Throws<ArgumentException>(() => new MigrationWorkflowEngine().Execute(request));
    }
}

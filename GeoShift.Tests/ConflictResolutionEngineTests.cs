using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class ConflictResolutionEngineTests
{
    private static (DatasetMergeResult Data, DuplicateDetectionResult Duplicates) Setup()
    {
        var data = new DatasetMergeEngine().Merge(
        [
            new MergeDataset("radio.csv",
            [
                new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "A", ["IP"] = "10.0.0.1" },
                new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "B", ["IP"] = "10.0.0.2" },
                new Dictionary<string, string?> { ["Site"] = "102", ["Radio"] = "C", ["IP"] = "10.0.0.3" }
            ])
        ]);
        return (data, new DuplicateDetectionEngine().Detect(data, ["Site"]));
    }

    [Fact]
    public void Resolve_LeavesAllRowsUntouchedWithoutDecisions()
    {
        var (data, duplicates) = Setup();
        var result = new ConflictResolutionEngine().Resolve(data, duplicates, []);
        Assert.Equal(3, result.Rows.Count);
        Assert.Empty(result.Audit);
    }

    [Fact]
    public void Resolve_KeepsSelectedRowAndTakesChosenColumnFromOtherRow()
    {
        var (data, duplicates) = Setup();
        var result = new ConflictResolutionEngine().Resolve(data, duplicates,
        [
            new ConflictResolutionDecision(0, new Dictionary<string, int> { ["IP"] = 1 })
        ]);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("A", result.Rows[0].Columns["Radio"]);
        Assert.Equal("10.0.0.2", result.Rows[0].Columns["IP"]);
        Assert.Equal("10.0.0.1", data.Rows[0].Columns["IP"]);
        var audit = Assert.Single(result.Audit);
        Assert.Equal([1], audit.RemovedRowIndices);
        Assert.Equal(1, audit.ColumnSources["IP"]);
    }

    [Fact]
    public void Resolve_RejectsColumnSelectionOutsideGroup()
    {
        var (data, duplicates) = Setup();
        Assert.Throws<InvalidDataException>(() => new ConflictResolutionEngine().Resolve(data, duplicates,
        [
            new ConflictResolutionDecision(0, new Dictionary<string, int> { ["IP"] = 2 })
        ]));
    }

    [Fact]
    public void Resolve_RejectsMultipleDecisionsForSameGroup()
    {
        var (data, duplicates) = Setup();
        Assert.Throws<InvalidDataException>(() => new ConflictResolutionEngine().Resolve(data, duplicates,
        [
            new ConflictResolutionDecision(0, new Dictionary<string, int>()),
            new ConflictResolutionDecision(1, new Dictionary<string, int>())
        ]));
    }

    [Fact]
    public void Resolve_RejectsDecisionForNonDuplicateRow()
    {
        var (data, duplicates) = Setup();
        Assert.Throws<InvalidDataException>(() => new ConflictResolutionEngine().Resolve(data, duplicates,
        [
            new ConflictResolutionDecision(2, new Dictionary<string, int>())
        ]));
    }
}

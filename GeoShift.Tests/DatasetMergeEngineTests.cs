using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class DatasetMergeEngineTests
{
    [Fact]
    public void Merge_UnionsColumnsAndPreservesProvenance()
    {
        var first = new Dictionary<string, string?> { ["Site ID"] = "101", ["Radio SN"] = "ABC" };
        var second = new Dictionary<string, string?> { ["site id"] = "102", ["Latitude"] = "29.9" };
        var result = new DatasetMergeEngine().Merge(
        [
            new MergeDataset("radio.csv", [first]),
            new MergeDataset("locations.csv", [second])
        ]);

        Assert.Equal(3, result.Columns.Count);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("radio.csv", result.Rows[0].SourceDataset);
        Assert.Equal(1, result.Rows[0].SourceRowNumber);
        Assert.Equal("locations.csv", result.Rows[1].SourceDataset);
        Assert.Equal("102", result.Rows[1].Columns["Site ID"]);
        Assert.Null(result.Rows[0].Columns["Latitude"]);
        Assert.Equal("101", first["Site ID"]);
    }

    [Fact]
    public void Merge_DoesNotDeduplicateIdenticalRecords()
    {
        var row = new Dictionary<string, string?> { ["Site"] = "1" };
        var result = new DatasetMergeEngine().Merge(
        [
            new MergeDataset("a.csv", [row, row]),
            new MergeDataset("b.csv", [row])
        ]);

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(2, result.Rows[1].SourceRowNumber);
        Assert.Equal("b.csv", result.Rows[2].SourceDataset);
    }

    [Fact]
    public void Merge_RejectsDuplicateDatasetNames()
    {
        var engine = new DatasetMergeEngine();
        Assert.Throws<InvalidDataException>(() => engine.Merge(
        [
            new MergeDataset("A.csv", []),
            new MergeDataset("a.CSV", [])
        ]));
    }

    [Fact]
    public void Merge_RespectsPreferredColumnOrder()
    {
        var result = new DatasetMergeEngine().Merge(
        [
            new MergeDataset("a", [new Dictionary<string, string?> { ["B"] = "2", ["A"] = "1" }])
        ], ["A", "B"]);

        Assert.Equal(["A", "B"], result.Columns);
    }

    [Fact]
    public void Merge_RejectsAmbiguousRowColumnNames()
    {
        var row = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Site"] = "1",
            ["site"] = "2"
        };
        Assert.Throws<InvalidDataException>(() => new DatasetMergeEngine().Merge(
        [
            new MergeDataset("a", [row])
        ]));
    }
}

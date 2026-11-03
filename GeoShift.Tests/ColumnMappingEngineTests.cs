using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class ColumnMappingEngineTests
{
    [Fact]
    public void Map_RenamesColumnsWithoutMutatingSource()
    {
        var source = new Dictionary<string, string?>
        {
            ["Site #"] = "1042",
            ["Radio SN"] = "ABC123",
            ["Latitude"] = "29.8"
        };
        var result = new ColumnMappingEngine().Map(source,
        [
            new ColumnMappingRule("Site #", "Location ID"),
            new ColumnMappingRule("Radio SN", "Serial Number")
        ]);

        Assert.Equal("1042", result.Columns["Location ID"]);
        Assert.Equal("ABC123", result.Columns["Serial Number"]);
        Assert.Equal("29.8", result.Columns["Latitude"]);
        Assert.False(result.Columns.ContainsKey("Site #"));
        Assert.Equal("1042", source["Site #"]);
        Assert.Equal(2, result.Changes.Count);
    }

    [Fact]
    public void Map_RejectsDuplicateTargetNamesIgnoringCase()
    {
        var source = new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" };
        Assert.Throws<InvalidDataException>(() => new ColumnMappingEngine().Map(source,
        [
            new ColumnMappingRule("A", "Site"),
            new ColumnMappingRule("B", "site")
        ]));
    }

    [Fact]
    public void Map_RejectsUnmappedCollisionInsteadOfOverwriting()
    {
        var source = new Dictionary<string, string?> { ["Site #"] = "1", ["Site ID"] = "2" };
        Assert.Throws<InvalidDataException>(() => new ColumnMappingEngine().Map(source,
        [
            new ColumnMappingRule("Site #", "Site ID")
        ]));
    }

    [Fact]
    public void Map_CanExcludeUnmappedColumns()
    {
        var source = new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" };
        var result = new ColumnMappingEngine().Map(source,
        [
            new ColumnMappingRule("A", "Output")
        ], includeUnmappedColumns: false);

        Assert.Single(result.Columns);
        Assert.Equal("1", result.Columns["Output"]);
    }

    [Fact]
    public void Map_RejectsMissingSourceColumn()
    {
        var source = new Dictionary<string, string?> { ["A"] = "1" };
        Assert.Throws<InvalidDataException>(() => new ColumnMappingEngine().Map(source,
        [
            new ColumnMappingRule("Missing", "Output")
        ]));
    }

    [Fact]
    public void Map_AllowsNullValues()
    {
        var source = new Dictionary<string, string?> { ["A"] = null };
        var result = new ColumnMappingEngine().Map(source,
        [
            new ColumnMappingRule("A", "Output")
        ]);
        Assert.Null(result.Columns["Output"]);
    }
}

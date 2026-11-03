using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class DuplicateDetectionEngineTests
{
    private static DatasetMergeResult Data(params IReadOnlyDictionary<string, string?>[] rows) =>
        new DatasetMergeEngine().Merge([new MergeDataset("inventory.csv", rows)]);

    [Fact]
    public void Detect_GroupsMatchingIdsAndReportsConflicts()
    {
        var data = Data(
            new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "ABC" },
            new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "DEF" },
            new Dictionary<string, string?> { ["Site"] = "102", ["Radio"] = "GHI" });
        var result = new DuplicateDetectionEngine().Detect(data, ["Site"]);
        var group = Assert.Single(result.Groups);
        Assert.Equal([0, 1], group.RowIndices);
        Assert.True(group.HasConflicts);
        Assert.Equal("Radio", Assert.Single(group.ConflictingColumns));
        Assert.Equal("101", group.Key["Site"]);
    }

    [Fact]
    public void Detect_IdenticalRowsHaveNoConflicts()
    {
        var row = new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "ABC" };
        var group = Assert.Single(new DuplicateDetectionEngine().Detect(Data(row, row), ["Site"]).Groups);
        Assert.False(group.HasConflicts);
    }

    [Fact]
    public void Detect_CompositeKeyDistinguishesDifferentAssets()
    {
        var data = Data(
            new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "A" },
            new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "B" },
            new Dictionary<string, string?> { ["Site"] = "101", ["Radio"] = "A" });
        var group = Assert.Single(new DuplicateDetectionEngine().Detect(data, ["Site", "Radio"]).Groups);
        Assert.Equal([0, 2], group.RowIndices);
    }

    [Fact]
    public void Detect_SkipsBlankKeysRatherThanTreatingThemAsDuplicates()
    {
        var data = Data(
            new Dictionary<string, string?> { ["Site"] = "" },
            new Dictionary<string, string?> { ["Site"] = null });
        var result = new DuplicateDetectionEngine().Detect(data, ["Site"]);
        Assert.Empty(result.Groups);
        Assert.Equal([0, 1], result.SkippedRowIndices);
    }

    [Fact]
    public void Detect_DefaultsToTrimmedCaseInsensitiveKeys()
    {
        var data = Data(
            new Dictionary<string, string?> { ["Site"] = " abC " },
            new Dictionary<string, string?> { ["Site"] = "ABC" });
        Assert.Single(new DuplicateDetectionEngine().Detect(data, ["Site"]).Groups);
        Assert.Empty(new DuplicateDetectionEngine().Detect(data, ["Site"], ignoreCase: false).Groups);
    }

    [Fact]
    public void Detect_RejectsUnknownKeyColumn()
    {
        var data = Data(new Dictionary<string, string?> { ["Site"] = "101" });
        Assert.Throws<ArgumentException>(() => new DuplicateDetectionEngine().Detect(data, ["Missing"]));
    }
}

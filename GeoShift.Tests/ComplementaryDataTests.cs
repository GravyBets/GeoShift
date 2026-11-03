using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class ComplementaryDataTests
{
    [Fact]
    public void MissingValueIsFlaggedAndCanBeSelectedWithoutChangingSource()
    {
        var merged = new DatasetMergeEngine().Merge(
        [
            new MergeDataset("Inventory", [
                new Dictionary<string, string?> { ["Site"] = "102", ["RadioSN"] = "XYZ999", ["CyberLockSN"] = "" }
            ]),
            new MergeDataset("Audit", [
                new Dictionary<string, string?> { ["Site"] = "102", ["RadioSN"] = "XYZ999", ["CyberLockSN"] = "CL12345" }
            ])
        ]);
        var duplicates = new DuplicateDetectionEngine().Detect(merged, ["Site"]);
        var group = Assert.Single(duplicates.Groups);
        Assert.Contains("CyberLockSN", group.ConflictingColumns);

        var untouched = new ConflictResolutionEngine().Resolve(merged, duplicates, []);
        Assert.Equal(2, untouched.Rows.Count);

        var resolved = new ConflictResolutionEngine().Resolve(merged, duplicates,
        [
            new ConflictResolutionDecision(0, new Dictionary<string, int> { ["CyberLockSN"] = 1 })
        ]);
        var row = Assert.Single(resolved.Rows);
        Assert.Equal("CL12345", row.Columns["CyberLockSN"]);
        Assert.Equal("", merged.Rows[0].Columns["CyberLockSN"]);
        Assert.Equal(1, Assert.Single(resolved.Audit).ColumnSources["CyberLockSN"]);
    }

    [Fact]
    public void UnselectedMissingValueIsNeverFilledAutomatically()
    {
        var merged = new DatasetMergeEngine().Merge(
        [
            new MergeDataset("Inventory", [
                new Dictionary<string, string?> { ["Site"] = "102", ["CyberLockSN"] = "" }
            ]),
            new MergeDataset("Audit", [
                new Dictionary<string, string?> { ["Site"] = "102", ["CyberLockSN"] = "CL12345" }
            ])
        ]);
        var duplicates = new DuplicateDetectionEngine().Detect(merged, ["Site"]);
        var resolved = new ConflictResolutionEngine().Resolve(merged, duplicates,
        [
            new ConflictResolutionDecision(0, new Dictionary<string, int>())
        ]);
        Assert.Equal("", Assert.Single(resolved.Rows).Columns["CyberLockSN"]);
    }
}

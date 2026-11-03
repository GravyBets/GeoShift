using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class MultiColumnMappingTests
{
    [Fact]
    public void DifferentSourceHeadersCanBecomeOneCanonicalColumn()
    {
        var request = new MigrationWorkflowRequest(
        [
            new MigrationWorkflowDataset("Inventory",
            [
                new Dictionary<string, string?> { ["Site"] = "102", ["RadioSerialNumber"] = "XYZ999" }
            ],
            [
                new ColumnMappingRule("Site", "GeoShift Match Key"),
                new ColumnMappingRule("RadioSerialNumber", "RadioSN")
            ]),
            new MigrationWorkflowDataset("Audit",
            [
                new Dictionary<string, string?> { ["SiteNumber"] = "102", ["Radio SN"] = "XYZ999" }
            ],
            [
                new ColumnMappingRule("SiteNumber", "GeoShift Match Key"),
                new ColumnMappingRule("Radio SN", "RadioSN")
            ])
        ], ["GeoShift Match Key"], []);
        var result = new MigrationWorkflowEngine().Execute(request);
        Assert.Contains("RadioSN", result.Merged.Columns);
        Assert.DoesNotContain("RadioSerialNumber", result.Merged.Columns);
        Assert.DoesNotContain("Radio SN", result.Merged.Columns);
        var group = Assert.Single(result.Duplicates.Groups);
        Assert.False(group.HasConflicts);
        Assert.Equal(2, result.Resolved.Rows.Count);
    }

    [Fact]
    public void MappingCollisionIsRejectedInsteadOfDroppingData()
    {
        var request = new MigrationWorkflowRequest(
        [
            new MigrationWorkflowDataset("Inventory",
            [
                new Dictionary<string, string?> { ["Site"] = "102", ["RadioSN"] = "A", ["Other"] = "B" }
            ],
            [
                new ColumnMappingRule("Site", "GeoShift Match Key"),
                new ColumnMappingRule("RadioSN", "Serial"),
                new ColumnMappingRule("Other", "Serial")
            ])
        ], ["GeoShift Match Key"], []);
        Assert.Throws<InvalidDataException>(() => new MigrationWorkflowEngine().Execute(request));
    }
}

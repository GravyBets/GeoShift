using GeoShift.Core.Services;
namespace GeoShift.Tests;
public sealed class MigrationCsvExporterTests
{
    [Fact]
    public void Cleaned_PreservesSelectedFieldAndQuotesCsv()
    {
        var rows = new[]
        {
            new MergeDataset("Inventory", new IReadOnlyDictionary<string,string?>[]
            {
                new Dictionary<string,string?> { ["Site"]="102", ["Serial"]="DEF,456" }
            }),
            new MergeDataset("Audit", new IReadOnlyDictionary<string,string?>[]
            {
                new Dictionary<string,string?> { ["Site"]="102", ["Serial"]="XYZ999" }
            })
        };
        var merged = new DatasetMergeEngine().Merge(rows);
        var duplicate = new DuplicateDetectionEngine().Detect(merged, ["Site"]);
        var resolved = new ConflictResolutionEngine().Resolve(merged, duplicate,
            [new ConflictResolutionDecision(0, new Dictionary<string,int> { ["Serial"]=1 })]);
        var csv = MigrationCsvExporter.Cleaned(merged, resolved);
        Assert.Contains("XYZ999", csv);
        Assert.DoesNotContain("DEF,456", csv);
        Assert.Equal(2, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        var audit = MigrationCsvExporter.Audit(merged, duplicate, resolved, []);
        Assert.Contains("Combined", audit);
        Assert.Contains("Audit row 1", audit);
    }

    [Fact]
    public void Cleaned_ProtectsSpreadsheetFormulas()
    {
        var merged = new DatasetMergeEngine().Merge(
            [new MergeDataset("a", new IReadOnlyDictionary<string,string?>[]
            {
                new Dictionary<string,string?> { ["Site"]="1", ["Note"]="=1+1" }
            })]);
        var duplicates = new DuplicateDetectionEngine().Detect(merged, ["Site"]);
        var resolved = new ConflictResolutionEngine().Resolve(merged, duplicates, []);
        Assert.Contains("'=1+1", MigrationCsvExporter.Cleaned(merged, resolved));
    }
}

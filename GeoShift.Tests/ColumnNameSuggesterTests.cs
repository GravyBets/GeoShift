using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class ColumnNameSuggesterTests
{
    [Fact]
    public void SuggestsEquivalentHeadersWithoutChangingInputs()
    {
        var inputs = new[]
        {
            new ColumnNameDataset(["Site", "RadioSN", "CyberLockSN"], "Site"),
            new ColumnNameDataset(["SiteNumber", "Radio Serial Number", "CyberLockSerial"], "SiteNumber")
        };
        var result = ColumnNameSuggester.Suggest(inputs);
        Assert.Contains(result, x => x.DatasetIndex == 1 && x.SourceColumn == "Radio Serial Number" && x.SuggestedOutputColumn == "RadioSN");
        Assert.Contains(result, x => x.DatasetIndex == 1 && x.SourceColumn == "CyberLockSerial" && x.SuggestedOutputColumn == "CyberLockSN");
        Assert.DoesNotContain(result, x => x.SourceColumn == "SiteNumber");
        Assert.Equal("Radio Serial Number", inputs[1].Columns[1]);
    }

    [Fact]
    public void DoesNotSuggestAmbiguousColumnsWithinOneFile()
    {
        var result = ColumnNameSuggester.Suggest([
            new ColumnNameDataset(["Site", "RadioSN"], "Site"),
            new ColumnNameDataset(["SiteNumber", "Radio Serial", "Radio Serial Number"], "SiteNumber")
        ]);
        Assert.Empty(result);
    }

    [Fact]
    public void DoesNotMatchUnrelatedHeaders()
    {
        var result = ColumnNameSuggester.Suggest([
            new ColumnNameDataset(["Site", "RadioSN"], "Site"),
            new ColumnNameDataset(["SiteNumber", "Latitude"], "SiteNumber")
        ]);
        Assert.Empty(result);
    }
}

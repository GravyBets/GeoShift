using GeoShift.Core.Services;

namespace GeoShift.Tests;

public class CsvPreviewReaderTests
{
    [Fact]
    public void Read_QuotedCommasAndNewlines_KeepColumnsAligned()
    {
        const string csv = "\"Name\",\"Description\"\r\n" +
                           "\"Site, Alpha\",\"First line\nSecond line\"\r\n";
        var rows = CsvPreviewReader.Read(csv);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Name", rows[0][0]);
        Assert.Equal("Description", rows[0][1]);
        Assert.Equal("Site, Alpha", rows[1][0]);
        Assert.Equal("First line\nSecond line", rows[1][1]);
    }

    [Fact]
    public void Read_DoubledQuotes_UnescapesValues()
    {
        var rows = CsvPreviewReader.Read("\"Name\"\n\"Site \"\"Alpha\"\"\"\n");
        Assert.Equal("Site \"Alpha\"", rows[1][0]);
    }

    [Fact]
    public void Read_LimitsNumberOfRecords()
    {
        var rows = CsvPreviewReader.Read("Name\nA\nB\nC\n", 3);
        Assert.Equal(3, rows.Count);
        Assert.Equal("B", rows[2][0]);
    }

    [Fact]
    public void Read_UnterminatedQuotedField_Throws()
    {
        Assert.Throws<InvalidDataException>(
            () => CsvPreviewReader.Read("\"Name\"\n\"Broken"));
    }
}

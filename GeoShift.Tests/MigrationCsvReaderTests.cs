using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class MigrationCsvReaderTests
{
    [Fact]
    public void Read_HandlesQuotedCommasAndNewlines()
    {
        using var input = new StringReader("Site,Notes\r\n101,\"hello, world\"\r\n102,\"line 1\nline 2\"\r\n");
        var rows = MigrationCsvReader.Read(input);
        Assert.Equal(2, rows.Count);
        Assert.Equal("hello, world", rows[0]["Notes"]);
        Assert.Equal("line 1\nline 2", rows[1]["Notes"]);
    }

    [Fact]
    public void Read_RejectsDuplicateHeaders()
    {
        using var input = new StringReader("Site,site\n101,102");
        Assert.Throws<InvalidDataException>(() => MigrationCsvReader.Read(input));
    }

    [Fact]
    public void Read_RejectsUnevenColumns()
    {
        using var input = new StringReader("Site,Radio\n101");
        Assert.Throws<InvalidDataException>(() => MigrationCsvReader.Read(input));
    }

    [Fact]
    public void Read_RejectsUnterminatedQuotes()
    {
        using var input = new StringReader("Site,Radio\n101,\"unfinished");
        Assert.Throws<InvalidDataException>(() => MigrationCsvReader.Read(input));
    }

    [Fact]
    public void Read_RejectsRowsBeyondLimit()
    {
        using var input = new StringReader("Site\n101\n102\n");
        Assert.Throws<InvalidDataException>(() => MigrationCsvReader.Read(input, maxRows: 1));
    }
}

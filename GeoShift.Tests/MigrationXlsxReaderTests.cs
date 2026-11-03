using System.IO.Compression;
using System.Text;
using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class MigrationXlsxReaderTests
{
    [Fact]
    public void ReadsSharedStringsAndInlineStringsFromFirstWorksheet()
    {
        using var stream = Workbook(false);
        var rows = MigrationXlsxReader.Read(stream);
        var row = Assert.Single(rows);
        Assert.Equal("102", row["Site"]);
        Assert.Equal("CL12345", row["CyberLockSN"]);
    }

    [Fact]
    public void RejectsFormulaCellsRatherThanTrustingCachedResults()
    {
        using var stream = Workbook(true);
        Assert.Throws<InvalidDataException>(() => MigrationXlsxReader.Read(stream));
    }

    private static MemoryStream Workbook(bool formula)
    {
        var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(zip, "xl/workbook.xml", "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Data' sheetId='1' r:id='rId1'/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Target='worksheets/sheet1.xml' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet'/></Relationships>");
            Add(zip, "xl/sharedStrings.xml", "<sst xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><si><t>Site</t></si><si><t>CyberLockSN</t></si></sst>");
            var value = formula ? "<c r='B2'><f>CONCAT(1,2)</f><v>12</v></c>" : "<c r='B2' t='inlineStr'><is><t>CL12345</t></is></c>";
            Add(zip, "xl/worksheets/sheet1.xml", "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='s'><v>0</v></c><c r='B1' t='s'><v>1</v></c></row><row r='2'><c r='A2'><v>102</v></c>" + value + "</row></sheetData></worksheet>");
        }
        output.Position = 0;
        return output;
    }

    private static void Add(ZipArchive zip, string path, string xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }
}

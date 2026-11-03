using System.Text;
using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class StreamingKmlCsvConverterTests
{
    [Fact]
    public void Convert_NestedFoldersAndCustomFields_PreservesRowsAndSummary()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document><Folder><name>Texas</name>
                <Folder><name>Houston</name>
                  <Placemark><name>Alpha</name>
                    <ExtendedData><Data name="Site ID"><value>TX-1</value></Data></ExtendedData>
                    <MultiGeometry>
                      <Point><coordinates>-95.3,29.7,-2</coordinates></Point>
                      <Point><coordinates>-95.4,29.8</coordinates></Point>
                      <LineString><coordinates>-95.3,29.7 -95.4,29.8</coordinates></LineString>
                    </MultiGeometry>
                  </Placemark>
                </Folder>
              </Folder></Document>
            </kml>
            """;
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(kml));
        using var output = new StringWriter();
        var result = new StreamingKmlCsvConverter().Convert(input, output);
        var csv = output.ToString();
        Assert.Equal(2, result.ExportedPointCount);
        Assert.Equal(0, result.SkippedPointCount);
        Assert.Equal(1, result.ExcludedLineStringCount);
        Assert.Equal(1, result.CustomColumnCount);
        Assert.Contains("\"Texas/Houston\"", csv);
        Assert.Contains("\"29.7\",\"-95.3\",\"-2\"", csv);
        Assert.Contains("\"29.8\",\"-95.4\",\"\"", csv);
        Assert.Contains("\"TX-1\"", csv);
    }

    [Fact]
    public void Convert_InvalidPointAndPolygon_ReportsExclusions()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Placemark><Point><coordinates>oops</coordinates></Point></Placemark>
              <Placemark><Polygon><outerBoundaryIs><LinearRing>
                <coordinates>-95,29 -96,29 -95,29</coordinates>
              </LinearRing></outerBoundaryIs></Polygon></Placemark>
            </kml>
            """;
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(kml));
        using var output = new StringWriter();
        var result = new StreamingKmlCsvConverter().Convert(input, output);
        Assert.Equal(0, result.ExportedPointCount);
        Assert.Equal(1, result.SkippedPointCount);
        Assert.Equal(1, result.ExcludedPolygonCount);
    }

    [Fact]
    public void Convert_RejectsNonSeekableInput()
    {
        using var input = new NonSeekableStream(Encoding.UTF8.GetBytes("<kml/>"));
        using var output = new StringWriter();
        Assert.Throws<InvalidDataException>(
            () => new StreamingKmlCsvConverter().Convert(input, output));
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}

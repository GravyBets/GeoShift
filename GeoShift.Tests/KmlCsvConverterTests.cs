
using System.Text;
using GeoShift.Core.Services;

namespace GeoShift.Tests;

public class KmlCsvConverterTests
{
    [Fact]
    public void Convert_ValidPoint_ReturnsCsv()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <name>Site Alpha</name>
                  <description>Sample site</description>
                  <Point>
                    <coordinates>-95.3698,29.7604,0</coordinates>
                  </Point>
                </Placemark>
              </Document>
            </kml>
            """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var converter = new KmlCsvConverter();
        var csv = converter.Convert(stream);

        Assert.Contains(
            "Name,Latitude,Longitude,Altitude,Description",
            csv);

        Assert.Contains("\"Site Alpha\"", csv);
        Assert.Contains("\"Sample site\"", csv);
        Assert.Contains("-95.3698", csv);
        Assert.Contains("29.7604", csv);
    }

    [Fact]
    public void Convert_SkipsPathsAndPolygons()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <name>Route A</name>
                  <LineString>
                    <coordinates>
                      -95.3,29.7 -95.4,29.8
                    </coordinates>
                  </LineString>
                </Placemark>
              </Document>
            </kml>
            """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var csv = new KmlCsvConverter().Convert(stream);

        Assert.DoesNotContain("Route A", csv);
        Assert.StartsWith("Name,Latitude,Longitude", csv);
    }
}

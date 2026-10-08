
using System.Text;
using System.IO.Compression;
using GeoShift.Core.Services;

namespace GeoShift.Tests;

public class KmlAnalyzerTests
{
    [Fact]
    public void Analyze_ValidKml_ReturnsCorrectCounts()
    {
        const string kml = """
            <?xml version="1.0" encoding="utf-8"?>
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Folder>
                  <name>Test Sites</name>

                  <Placemark>
                    <name>Site A</name>
                    <Point>
                      <coordinates>-95.3698,29.7604,0</coordinates>
                    </Point>
                  </Placemark>

                  <Placemark>
                    <name>Route B</name>
                    <LineString>
                      <coordinates>
                        -95.3698,29.7604,0
                        -95.3600,29.7700,0
                      </coordinates>
                    </LineString>
                  </Placemark>

                  <Placemark>
                    <name>Site C</name>
                    <Point>
                      <coordinates>-95.3500,29.7800,0</coordinates>
                    </Point>
                  </Placemark>
                </Folder>
              </Document>
            </kml>
            """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var analyzer = new KmlAnalyzer();

        var result = analyzer.Analyze(stream);

        Assert.Equal(3, result.PlacemarkCount);
        Assert.Equal(2, result.PointCount);
        Assert.Equal(1, result.LineStringCount);
        Assert.Equal(0, result.PolygonCount);
        Assert.Equal(1, result.FolderCount);
    }


    [Fact]
    public void Analyze_ValidKmz_ReturnsCorrectCounts()
    {
        const string kml = """
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Placemark>
              <name>Test Site</name>
              <Point>
                <coordinates>-95.3698,29.7604,0</coordinates>
              </Point>
            </Placemark>
          </Document>
        </kml>
        """;

        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(
            memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("doc.kml");

            using var writer = new StreamWriter(entry.Open());
            writer.Write(kml);
        }

        memory.Position = 0;

        var analyzer = new KmzAnalyzer();
        var result = analyzer.Analyze(memory);

        Assert.Equal(1, result.PlacemarkCount);
        Assert.Equal(1, result.PointCount);
        Assert.Equal(0, result.LineStringCount);
        Assert.Equal(0, result.PolygonCount);
    }

}

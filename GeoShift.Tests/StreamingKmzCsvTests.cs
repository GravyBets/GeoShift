using System.IO.Compression;
using System.Text;
using GeoShift.Core.Services;

namespace GeoShift.Tests;

public sealed class StreamingKmzCsvTests
{
    [Fact]
    public void ConvertToCsvStreaming_ExportsPointsFromKmz()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document><Folder><name>Dispatch</name>
                <Placemark><name>RX100</name>
                  <Point><coordinates>-95.3,29.7,0</coordinates></Point>
                </Placemark>
              </Folder></Document>
            </kml>
            """;

        using var kmz = new MemoryStream();
        using (var archive = new ZipArchive(kmz, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = archive.CreateEntry("doc.kml").Open();
            var bytes = Encoding.UTF8.GetBytes(kml);
            entry.Write(bytes);
        }
        kmz.Position = 0;

        using var output = new StringWriter();
        var result = new KmzAnalyzer().ConvertToCsvStreaming(kmz, output);

        Assert.Equal(1, result.ExportedPointCount);
        Assert.Equal(0, result.SkippedPointCount);
        Assert.Contains("\"RX100\"", output.ToString());
        Assert.Contains("\"Dispatch\"", output.ToString());
        Assert.Contains("\"29.7\",\"-95.3\",\"0\"", output.ToString());
    }
}

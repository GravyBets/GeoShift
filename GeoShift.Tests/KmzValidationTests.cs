using System.IO.Compression;
using System.Text;
using GeoShift.Core.Services;

namespace GeoShift.Tests;

public class KmzValidationTests
{
    [Fact]
    public void Analyze_ArchiveWithoutKml_RejectsArchive()
    {
        using var archiveStream = CreateArchive(("readme.txt", "No KML here"));

        var error = Assert.Throws<InvalidDataException>(
            () => new KmzAnalyzer().Analyze(archiveStream));

        Assert.Contains("does not contain a KML file", error.Message);
    }

    [Fact]
    public void Analyze_OversizedDecompressedKml_RejectsArchive()
    {
        // Highly compressible input keeps the KMZ small while exceeding
        // the 20 MiB limit on the uncompressed KML entry.
        var oversizedKml = new string('x', 20 * 1024 * 1024 + 1);
        using var archiveStream = CreateArchive(("doc.kml", oversizedKml));

        var error = Assert.Throws<InvalidDataException>(
            () => new KmzAnalyzer().Analyze(archiveStream));

        Assert.Contains("20 MB limit", error.Message);
    }

    [Fact]
    public void Analyze_MultipleKmlEntries_PrefersRootDocKml()
    {
        const string firstKml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document />
            </kml>
            """;

        const string docKml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <Point><coordinates>-95.3,29.7</coordinates></Point>
                </Placemark>
              </Document>
            </kml>
            """;

        using var archiveStream = CreateArchive(
            ("other.kml", firstKml),
            ("doc.kml", docKml));

        var result = new KmzAnalyzer().Analyze(archiveStream);

        Assert.Equal(1, result.PointCount);
        Assert.Equal(1, result.PlacemarkCount);
    }

    [Fact]
    public void Analyze_CorruptedArchive_RejectsArchive()
    {
        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("not a ZIP archive"));

        Assert.Throws<InvalidDataException>(
            () => new KmzAnalyzer().Analyze(stream));
    }

    [Fact]
    public void Analyze_WithoutDocKml_UsesFirstKmlEntry()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <Point><coordinates>-95.3,29.7</coordinates></Point>
                </Placemark>
              </Document>
            </kml>
            """;

        using var archiveStream = CreateArchive(
            ("notes.txt", "Metadata"),
            ("nested/sites.kml", kml));

        var result = new KmzAnalyzer().Analyze(archiveStream);

        Assert.Equal(1, result.PointCount);
        Assert.Equal(1, result.PlacemarkCount);
    }

    [Fact]
    public void Analyze_KmlWithDtd_RejectsExternalEntities()
    {
        const string kml = """
            <!DOCTYPE kml [
              <!ENTITY external SYSTEM "file:///nonexistent-geoshift-secret">
            ]>
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <name>&external;</name>
              </Document>
            </kml>
            """;

        using var archiveStream = CreateArchive(("doc.kml", kml));

        Assert.Throws<System.Xml.XmlException>(
            () => new KmzAnalyzer().Analyze(archiveStream));
    }

    private static MemoryStream CreateArchive(
        params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(
            stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(
                    name, CompressionLevel.Optimal);

                using var writer = new StreamWriter(
                    entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }
}

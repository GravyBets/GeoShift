
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
            "\"Name\",\"Folder Path\",\"Latitude\",\"Longitude\",\"Altitude\",\"Description\"",
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
        Assert.StartsWith(
            "\"Name\",\"Folder Path\",\"Latitude\",\"Longitude\"",
            csv);
    }

    [Fact]
    public void Convert_NestedFolders_PreservesFullFolderPath()
    {
        const string kml = """
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Folder>
              <name>Texas</name>
              <Folder>
                <name>Houston</name>
                <Folder>
                  <name>Radio Sites</name>
                  <Placemark>
                    <name>Site Alpha</name>
                    <Point>
                      <coordinates>-95.3698,29.7604,0</coordinates>
                    </Point>
                  </Placemark>
                </Folder>
              </Folder>
            </Folder>
          </Document>
        </kml>
        """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var converter = new KmlCsvConverter();
        var csv = converter.Convert(stream);

        Assert.Contains(
            "\"Texas/Houston/Radio Sites\"",
            csv);

        Assert.Contains("\"Site Alpha\"", csv);
    }

    [Fact]
    public void Convert_CustomAttributes_CreatesDynamicColumns()
    {
        const string kml = """
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Placemark>
              <name>Site Alpha</name>
              <ExtendedData>
                <Data name="Site ID">
                  <value>TX-001</value>
                </Data>
                <Data name="Equipment Type">
                  <value>Radio</value>
                </Data>
              </ExtendedData>
              <Point>
                <coordinates>-95.3698,29.7604,0</coordinates>
              </Point>
            </Placemark>

            <Placemark>
              <name>Site Bravo</name>
              <ExtendedData>
                <SchemaData schemaUrl="#sites">
                  <SimpleData name="Status">
                    Maintenance
                  </SimpleData>
                </SchemaData>
              </ExtendedData>
              <Point>
                <coordinates>-96.7970,32.7767,0</coordinates>
              </Point>
            </Placemark>
          </Document>
        </kml>
        """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var csv = new KmlCsvConverter().Convert(stream);

        // All three custom columns must be discovered.
        Assert.Contains("\"Site ID\"", csv);
        Assert.Contains("\"Equipment Type\"", csv);
        Assert.Contains("\"Status\"", csv);

        // The correct values must be preserved.
        Assert.Contains("\"TX-001\"", csv);
        Assert.Contains("\"Radio\"", csv);
        Assert.Contains("\"Maintenance\"", csv);

        // Both point records must be present.
        Assert.Contains("\"Site Alpha\"", csv);
        Assert.Contains("\"Site Bravo\"", csv);
    }

    [Fact]
    public void Convert_DifferentAttributes_AlignsColumnsCorrectly()
    {
        const string kml = """
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Placemark>
              <name>Site Alpha</name>
              <ExtendedData>
                <Data name="Site ID">
                  <value>TX-001</value>
                </Data>
                <Data name="Equipment Type">
                  <value>Radio</value>
                </Data>
              </ExtendedData>
              <Point>
                <coordinates>-95.3698,29.7604,0</coordinates>
              </Point>
            </Placemark>

            <Placemark>
              <name>Site Bravo</name>
              <ExtendedData>
                <Data name="Status">
                  <value>Maintenance</value>
                </Data>
              </ExtendedData>
              <Point>
                <coordinates>-96.7970,32.7767,0</coordinates>
              </Point>
            </Placemark>
          </Document>
        </kml>
        """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var csv = new KmlCsvConverter().Convert(stream);

        System.Diagnostics.Debug.WriteLine(
            "ACTUAL CSV:\n" + csv);

        var lines = csv.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();

        Assert.Equal(3, lines.Length);

        // Standard columns followed by dynamically discovered fields.
        Assert.Equal(
            "\"Name\",\"Folder Path\",\"Latitude\",\"Longitude\"," +
            "\"Altitude\",\"Description\",\"Site ID\"," +
            "\"Equipment Type\",\"Status\"",
            lines[0]);

        // Alpha has Site ID and Equipment Type, but no Status.
        Assert.Equal(
            "\"Site Alpha\",\"\",\"29.7604\",\"-95.3698\"," +
            "\"0\",\"\",\"TX-001\",\"Radio\",\"\"",
            lines[1]);

        // Bravo has Status, but no Site ID or Equipment Type.
        Assert.Equal(
            "\"Site Bravo\",\"\",\"32.7767\",\"-96.797\"," +
            "\"0\",\"\",\"\",\"\",\"Maintenance\"",
            lines[2]);
    }

    [Fact]
    public void Convert_NegativeCoordinatesAndFormulaText_HandlesBothSafely()
    {
        const string kml = """
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Placemark>
              <name>=HYPERLINK("https://example.com")</name>
              <description>Normal description</description>
              <ExtendedData>
                <Data name="Notes">
                  <value>+SUM(1,2)</value>
                </Data>
              </ExtendedData>
              <Point>
                <coordinates>-95.3698,29.7604,-12.5</coordinates>
              </Point>
            </Placemark>
          </Document>
        </kml>
        """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var csv = new KmlCsvConverter().Convert(stream);

        // Numeric coordinates must remain numeric.
        Assert.Contains(
            "\"29.7604\",\"-95.3698\",\"-12.5\"",
            csv);

        // Potential spreadsheet formulas must be escaped.
        Assert.Contains(
            "\"'=HYPERLINK(\"\"https://example.com\"\")\"",
            csv);

        Assert.Contains(
            "\"'+SUM(1,2)\"",
            csv);
    }

    [Fact]
    public void ConvertWithSummary_MixedGeometry_ReportsCorrectCounts()
    {
        const string kml = """
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Placemark>
              <name>Valid Site</name>
              <Point>
                <coordinates>-95.3698,29.7604,0</coordinates>
              </Point>
            </Placemark>

            <Placemark>
              <name>Invalid Site</name>
              <Point>
                <coordinates>invalid,coordinates</coordinates>
              </Point>
            </Placemark>

            <Placemark>
              <name>Missing Coordinates</name>
              <Point />
            </Placemark>

            <Placemark>
              <name>Route A</name>
              <LineString>
                <coordinates>
                  -95.3,29.7 -95.4,29.8
                </coordinates>
              </LineString>
            </Placemark>

            <Placemark>
              <name>Area A</name>
              <Polygon>
                <outerBoundaryIs>
                  <LinearRing>
                    <coordinates>
                      -95.3,29.7 -95.4,29.7
                      -95.4,29.8 -95.3,29.7
                    </coordinates>
                  </LinearRing>
                </outerBoundaryIs>
              </Polygon>
            </Placemark>
          </Document>
        </kml>
        """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var converter = new KmlCsvConverter();
        var result = converter.ConvertWithSummary(stream);

        Assert.Equal(1, result.ExportedPointCount);
        Assert.Equal(2, result.SkippedPointCount);
        Assert.Equal(1, result.ExcludedLineStringCount);
        Assert.Equal(1, result.ExcludedPolygonCount);
        Assert.Equal(0, result.CustomColumnCount);

        Assert.True(result.HasWarnings);

        Assert.Contains("\"Valid Site\"", result.CsvContent);
        Assert.DoesNotContain("\"Invalid Site\"", result.CsvContent);
        Assert.DoesNotContain("\"Missing Coordinates\"", result.CsvContent);
        Assert.DoesNotContain("\"Route A\"", result.CsvContent);
        Assert.DoesNotContain("\"Area A\"", result.CsvContent);
    }

    [Fact]
    public void ConvertWithSummary_PointWithMultipleCoordinateTuples_IsSkipped()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Document>
                <Placemark>
                  <name>Ambiguous Point</name>
                  <Point>
                    <coordinates>-95.3,29.7 -95.4,29.8</coordinates>
                  </Point>
                </Placemark>
                <Placemark>
                  <name>Valid Point</name>
                  <Point>
                    <coordinates>-95.5,29.9</coordinates>
                  </Point>
                </Placemark>
              </Document>
            </kml>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(kml));
        var result = new KmlCsvConverter().ConvertWithSummary(stream);

        Assert.Equal(1, result.ExportedPointCount);
        Assert.Equal(1, result.SkippedPointCount);
        Assert.True(result.HasWarnings);
        Assert.Contains("\"Valid Point\"", result.CsvContent);
        Assert.DoesNotContain("\"Ambiguous Point\"", result.CsvContent);
    }

    [Fact]
    public void ConvertWithSummary_PointWithWhitespaceAroundSingleTuple_Exports()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2">
              <Placemark>
                <name>Whitespace Point</name>
                <Point>
                  <coordinates>
                    -95.3,29.7,0
                  </coordinates>
                </Point>
              </Placemark>
            </kml>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(kml));
        var result = new KmlCsvConverter().ConvertWithSummary(stream);

        Assert.Equal(1, result.ExportedPointCount);
        Assert.Equal(0, result.SkippedPointCount);
        Assert.Contains("\"Whitespace Point\"", result.CsvContent);
    }

    [Fact]
    public void ConvertWithSummary_MultiGeometry_ExportsAllPoints()
    {
        const string kml = """
        <kml xmlns="http://www.opengis.net/kml/2.2">
          <Document>
            <Folder>
              <name>Houston Sites</name>

              <Placemark>
                <name>Site Alpha</name>
                <description>Multiple radio locations</description>

                <ExtendedData>
                  <Data name="Site ID">
                    <value>TX-001</value>
                  </Data>
                </ExtendedData>

                <MultiGeometry>
                  <Point>
                    <coordinates>-95.3698,29.7604,0</coordinates>
                  </Point>

                  <Point>
                    <coordinates>-95.38,29.77,10</coordinates>
                  </Point>
                </MultiGeometry>
              </Placemark>

            </Folder>
          </Document>
        </kml>
        """;

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(kml));

        var converter = new KmlCsvConverter();
        var result = converter.ConvertWithSummary(stream);

        Assert.Equal(2, result.ExportedPointCount);
        Assert.Equal(0, result.SkippedPointCount);
        Assert.Equal(1, result.CustomColumnCount);
        Assert.False(result.HasWarnings);

        var lines = result.CsvContent.Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries);

        // Header + two exported point records.
        Assert.Equal(3, lines.Length);

        Assert.Equal(
            "\"Name\",\"Folder Path\",\"Latitude\",\"Longitude\"," +
            "\"Altitude\",\"Description\",\"Site ID\"",
            lines[0]);

        Assert.Equal(
            "\"Site Alpha\",\"Houston Sites\",\"29.7604\"," +
            "\"-95.3698\",\"0\",\"Multiple radio locations\",\"TX-001\"",
            lines[1]);

        Assert.Equal(
            "\"Site Alpha\",\"Houston Sites\",\"29.77\"," +
            "\"-95.38\",\"10\",\"Multiple radio locations\",\"TX-001\"",
            lines[2]);
    }
}

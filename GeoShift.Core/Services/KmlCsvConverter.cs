
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using GeoShift.Core.Models;

namespace GeoShift.Core.Services;

public sealed class KmlCsvConverter
{
    private static readonly XNamespace Kml =
        "http://www.opengis.net/kml/2.2";

    private static readonly string[] StandardColumns =
    [
        "Name",
        "Folder Path",
        "Latitude",
        "Longitude",
        "Altitude",
        "Description"
    ];


    public string Convert(Stream stream)
    {
        return ConvertWithSummary(stream).CsvContent;
    }

    public KmlCsvConversionResult ConvertWithSummary(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);


        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 20_000_000
        };

        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader);

        if (document.Root?.Name != Kml + "kml")
        {
            throw new InvalidDataException(
                "The file is not a supported KML 2.2 document.");
        }

        // Read all placemarks once.
        var placemarks = document
            .Descendants(Kml + "Placemark")
            .ToList();

        // Discover custom fields dynamically.
        // Only point placemarks are exported in V1.
        var customColumns = new List<(string SourceName, string Header)>();
        var seenSourceNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var reservedHeaders = new HashSet<string>(
            StandardColumns,
            StringComparer.OrdinalIgnoreCase);

        // Discover all custom names first, so a generated header cannot
        // take the name of another customer's actual custom field.
        var customNames = new List<string>();
        foreach (var placemark in placemarks)
        {
            if (!placemark.Descendants(Kml + "Point").Any())
                continue;

            var attributes =
                KmlAttributeReader.ReadAttributes(placemark);

            foreach (var name in attributes.Keys)
            {
                if (seenSourceNames.Add(name))
                    customNames.Add(name);
            }
        }

        foreach (var name in customNames)
            reservedHeaders.Add(name);

        var usedHeaders = new HashSet<string>(
            StandardColumns,
            StringComparer.OrdinalIgnoreCase);

        foreach (var name in customNames)
        {
            var header = name;
            if (!usedHeaders.Add(header))
            {
                var baseHeader = "ExtendedData: " + name;
                header = baseHeader;
                int suffix = 2;

                while (reservedHeaders.Contains(header) ||
                       !usedHeaders.Add(header))
                {
                    header = baseHeader + " (" + suffix++ + ")";
                }
            }

            customColumns.Add((name, header));
        }

        int exportedPoints = 0;
        int skippedPoints = 0;

        int excludedPaths = document
            .Descendants(Kml + "LineString")
            .Count();

        int excludedPolygons = document
            .Descendants(Kml + "Polygon")
            .Count();

        var csv = new StringBuilder();

        csv.AppendLine(string.Join(",",
            StandardColumns.Concat(customColumns.Select(column => column.Header))
                .Select(EscapeCsv)));

        foreach (var placemark in placemarks)
        {
            var points = placemark.Descendants(Kml + "Point");

            foreach (var point in points)
            {

                var coordinates =
                point.Element(Kml + "coordinates")?.Value;

            if (string.IsNullOrWhiteSpace(coordinates))
            {
                skippedPoints++;
                continue;
            }

            var coordinateTuples = coordinates.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            // A KML Point must have exactly one coordinate tuple.
            // Never silently discard additional tuples.
            if (coordinateTuples.Length != 1)
            {
                skippedPoints++;
                continue;
            }

            var parts = coordinateTuples[0].Split(',');

            // KML Point coordinates require longitude,latitude[,altitude].
            // Reject extra fields rather than silently discarding them.
            if (parts.Length is < 2 or > 3 ||
                !double.TryParse(
                    parts[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var longitude) ||
                !double.TryParse(
                    parts[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var latitude))
            {
                skippedPoints++;
                continue;
            }

            if (!double.IsFinite(longitude) ||
                !double.IsFinite(latitude) ||
                longitude is < -180 or > 180 ||
                latitude is < -90 or > 90)
            {
                skippedPoints++;
                continue;
            }

            var altitude = "";

            if (parts.Length == 3)
            {
                if (!double.TryParse(
                        parts[2],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsedAltitude) ||
                    !double.IsFinite(parsedAltitude))
                {
                    skippedPoints++;
                    continue;
                }

                altitude = parsedAltitude.ToString(
                    "G", CultureInfo.InvariantCulture);
            }

            var name =
                placemark.Element(Kml + "name")?.Value ?? "";

            var description =
                placemark.Element(Kml + "description")?.Value ?? "";

            var folderPath = GetFolderPath(placemark);

            var attributes =
                KmlAttributeReader.ReadAttributes(placemark);

            var values = new List<string>
            {
                name,
                folderPath,
                latitude.ToString(
                    "G", CultureInfo.InvariantCulture),
                longitude.ToString(
                    "G", CultureInfo.InvariantCulture),
                altitude,
                description
            };

            foreach (var column in customColumns)
            {
                values.Add(
                    attributes.GetValueOrDefault(column.SourceName) ?? "");
            }


            csv.AppendLine(string.Join(",",
                values.Select((value, index) =>
                    index is 2 or 3 or 4
                        ? QuoteCsv(value)
                        : EscapeCsv(value))));

            exportedPoints++;
            }

        }


        return new KmlCsvConversionResult
        {
            CsvContent = csv.ToString(),
            ExportedPointCount = exportedPoints,
            SkippedPointCount = skippedPoints,
            ExcludedLineStringCount = excludedPaths,
            ExcludedPolygonCount = excludedPolygons,
            CustomColumnCount = customColumns.Count
        };

    }

    private static string GetFolderPath(XElement placemark)
    {
        var folders = placemark
            .Ancestors(Kml + "Folder")
            .Reverse()
            .Select(folder =>
                folder.Element(Kml + "name")?.Value?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name));

        return string.Join("/", folders);
    }

    private static string QuoteCsv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeCsv(string value)
    {
        // Basic spreadsheet formula-injection protection.
        var trimmed = value.TrimStart();

        if (trimmed.Length > 0 &&
            "=+-@\t\r\n".Contains(trimmed[0]))
        {
            value = "'" + value;
        }

        return QuoteCsv(value);
    }

}

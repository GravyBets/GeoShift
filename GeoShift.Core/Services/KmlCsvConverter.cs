
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GeoShift.Core.Services;

public sealed class KmlCsvConverter
{
    private static readonly XNamespace Kml =
        "http://www.opengis.net/kml/2.2";

    public string Convert(Stream stream)
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

        var csv = new StringBuilder();

        csv.AppendLine(
            "Name,Latitude,Longitude,Altitude,Description");

        foreach (var placemark in
                 document.Descendants(Kml + "Placemark"))
        {
            // V1 exports only direct Point geometry.
            // Paths, polygons and nested geometry are not exported.
            var point = placemark.Element(Kml + "Point");

            if (point is null)
                continue;

            var coordinates = point
                .Element(Kml + "coordinates")?.Value;

            if (string.IsNullOrWhiteSpace(coordinates))
                continue;

            var coordinate = coordinates
                .Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (coordinate is null)
                continue;

            var parts = coordinate.Split(',');

            if (parts.Length < 2 ||
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
                continue;
            }

            if (!double.IsFinite(longitude) ||
                !double.IsFinite(latitude) ||
                longitude is < -180 or > 180 ||
                latitude is < -90 or > 90)
            {
                continue;
            }

            var altitude = "";

            if (parts.Length >= 3 &&
                double.TryParse(
                    parts[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsedAltitude) &&
                double.IsFinite(parsedAltitude))
            {
                altitude = parsedAltitude.ToString(
                    "G", CultureInfo.InvariantCulture);
            }

            var name =
                placemark.Element(Kml + "name")?.Value ?? "";

            var description =
                placemark.Element(Kml + "description")?.Value ?? "";


            csv.AppendLine(string.Join(",",
                EscapeCsv(name),
                latitude.ToString(
                    "G", CultureInfo.InvariantCulture),
                longitude.ToString(
                    "G", CultureInfo.InvariantCulture),
                altitude,
                EscapeCsv(description)));

        }

        return csv.ToString();
    }

    private static string EscapeCsv(string value)
    {
        // Prevent spreadsheet formula execution on import.
        var trimmed = value.TrimStart();

        if (trimmed.Length > 0 &&
            "=+-@\t\r\n".Contains(trimmed[0]))
        {
            value = "'" + value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

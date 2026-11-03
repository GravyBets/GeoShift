using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using GeoShift.Core.Models;

namespace GeoShift.Core.Services;

/// <summary>
/// Two-pass, bounded-memory KML-to-CSV conversion.
/// Requires a readable, seekable KML input. The output is written incrementally.
/// </summary>
public sealed class StreamingKmlCsvConverter
{
    private static readonly XNamespace Kml = "http://www.opengis.net/kml/2.2";
    private static readonly string[] StandardColumns =
        ["Name", "Folder Path", "Latitude", "Longitude", "Altitude", "Description"];

    public KmlCsvConversionResult Convert(Stream input, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (!input.CanRead || !input.CanSeek)
            throw new InvalidDataException("Streaming CSV conversion requires a readable, seekable KML stream.");

        var sourceNames = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Scan(input, (placemark, _) =>
        {
            if (!placemark.Descendants(Kml + "Point").Any()) return;
            foreach (var key in KmlAttributeReader.ReadAttributes(placemark).Keys)
                if (seen.Add(key)) sourceNames.Add(key);
        });

        var reserved = new HashSet<string>(StandardColumns, StringComparer.OrdinalIgnoreCase);
        reserved.UnionWith(sourceNames);
        var used = new HashSet<string>(StandardColumns, StringComparer.OrdinalIgnoreCase);
        var headers = new List<string>();
        foreach (var name in sourceNames)
        {
            var header = name;
            if (!used.Add(header))
            {
                var baseName = "ExtendedData: " + name;
                header = baseName;
                var suffix = 2;
                while (reserved.Contains(header) || !used.Add(header))
                    header = baseName + " (" + suffix++ + ")";
            }
            headers.Add(header);
        }

        output.WriteLine(string.Join(",", StandardColumns.Concat(headers).Select(EscapeCsv)));

        int exported = 0, skipped = 0, paths = 0, polygons = 0;
        Scan(input, (placemark, folderPath) =>
        {
            var attributes = KmlAttributeReader.ReadAttributes(placemark);
            var name = placemark.Element(Kml + "name")?.Value ?? "";
            var description = placemark.Element(Kml + "description")?.Value ?? "";
            foreach (var point in placemark.Descendants(Kml + "Point"))
            {
                if (!TryCoordinates(point, out var latitude, out var longitude, out var altitude))
                {
                    skipped++;
                    continue;
                }

                var values = new List<string>
                {
                    name, folderPath, latitude, longitude, altitude, description
                };
                foreach (var column in sourceNames)
                    values.Add(attributes.GetValueOrDefault(column) ?? "");

                output.WriteLine(string.Join(",", values.Select((value, index) =>
                    index is 2 or 3 or 4 ? Quote(value) : EscapeCsv(value))));
                exported++;
            }
        }, () => paths++, () => polygons++);

        output.Flush();
        return new KmlCsvConversionResult
        {
            ExportedPointCount = exported,
            SkippedPointCount = skipped,
            ExcludedLineStringCount = paths,
            ExcludedPolygonCount = polygons,
            CustomColumnCount = sourceNames.Count
        };
    }

    private static void Scan(
        Stream input,
        Action<XElement, string> onPlacemark,
        Action? onPath = null,
        Action? onPolygon = null)
    {
        input.Position = 0;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 200L * 1024 * 1024,
            IgnoreWhitespace = true
        };
        var nameTable = new NameTable();
        var namespaces = new XmlNamespaceManager(nameTable);
        namespaces.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
        var context = new XmlParserContext(nameTable, namespaces, null, XmlSpace.None);
        using var reader = XmlReader.Create(input, settings, context);
        reader.MoveToContent();
        if (reader.NodeType != XmlNodeType.Element ||
            reader.LocalName != "kml" || reader.NamespaceURI != Kml.NamespaceName)
            throw new InvalidDataException("The file is not a supported KML 2.2 document.");

        var folders = new Stack<(int Depth, string Name)>();
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement &&
                reader.NamespaceURI == Kml.NamespaceName &&
                reader.LocalName == "Folder")
            {
                if (folders.Count > 0 && folders.Peek().Depth == reader.Depth)
                    folders.Pop();
                continue;
            }

            if (reader.NodeType != XmlNodeType.Element ||
                reader.NamespaceURI != Kml.NamespaceName)
                continue;

            if (reader.LocalName == "Folder")
            {
                folders.Push((reader.Depth, ""));
            }
            else if (reader.LocalName == "name" &&
                     folders.Count > 0 && reader.Depth == folders.Peek().Depth + 1)
            {
                var folder = folders.Pop();
                folders.Push((folder.Depth, reader.ReadElementContentAsString().Trim()));
            }
            else if (reader.LocalName == "Placemark")
            {
                using var subtree = reader.ReadSubtree();
                subtree.MoveToContent();
                var placemark = XElement.Load(subtree);
                foreach (var _ in placemark.Descendants(Kml + "LineString")) onPath?.Invoke();
                foreach (var _ in placemark.Descendants(Kml + "Polygon")) onPolygon?.Invoke();
                var folderPath = string.Join("/",
                    folders.Reverse().Select(f => f.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n)));
                onPlacemark(placemark, folderPath);
            }
            else if (reader.LocalName == "LineString")
            {
                onPath?.Invoke();
            }
            else if (reader.LocalName == "Polygon")
            {
                onPolygon?.Invoke();
            }
        }
    }

    private static bool TryCoordinates(XElement point,
        out string latitude, out string longitude, out string altitude)
    {
        latitude = longitude = altitude = "";
        var raw = point.Element(Kml + "coordinates")?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var tuples = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tuples.Length != 1) return false;
        var parts = tuples[0].Split(',');
        if (parts.Length is < 2 or > 3 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.IsFinite(lon) || !double.IsFinite(lat) ||
            lon is < -180 or > 180 || lat is < -90 or > 90) return false;
        if (parts.Length == 3)
        {
            if (!double.TryParse(parts[2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var alt) || !double.IsFinite(alt))
                return false;
            altitude = alt.ToString("G", CultureInfo.InvariantCulture);
        }
        latitude = lat.ToString("G", CultureInfo.InvariantCulture);
        longitude = lon.ToString("G", CultureInfo.InvariantCulture);
        return true;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string EscapeCsv(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+-@\t\r\n".Contains(trimmed[0]))
            value = "'" + value;
        return Quote(value);
    }
}

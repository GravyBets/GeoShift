
using System.Xml.Linq;

namespace GeoShift.Core.Services;

public static class KmlAttributeReader
{
    private static readonly XNamespace Kml =
        "http://www.opengis.net/kml/2.2";

    public static Dictionary<string, string> ReadAttributes(
        XElement placemark)
    {
        ArgumentNullException.ThrowIfNull(placemark);

        var attributes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        var extendedData = placemark.Element(
            Kml + "ExtendedData");

        if (extendedData is null)
            return attributes;

        // Standard ExtendedData/Data elements.
        foreach (var data in extendedData.Elements(Kml + "Data"))
        {
            var name = data.Attribute("name")?.Value?.Trim();
            var value = data.Element(Kml + "value")?.Value.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(name))
                attributes[name] = value;
        }

        // SchemaData/SimpleData elements.
        foreach (var schemaData in
                 extendedData.Elements(Kml + "SchemaData"))
        {
            foreach (var data in
                     schemaData.Elements(Kml + "SimpleData"))
            {
                var name = data.Attribute("name")?.Value?.Trim();

                if (!string.IsNullOrWhiteSpace(name))
                    attributes[name] = data.Value.Trim();
            }
        }

        return attributes;
    }
}

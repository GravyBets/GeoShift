using System.Xml;
using GeoShift.Core.Models;

namespace GeoShift.Core.Services;

public sealed class KmlAnalyzer
{
    private const string KmlNamespace = "http://www.opengis.net/kml/2.2";

    public KmlAnalysisResult Analyze(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersInDocument = 0 // Byte limits are enforced at the KMZ extraction boundary.
        };

        // The XML parser context supplies the standard xsi prefix for exports
        // that reference xsi:schemaLocation without declaring xmlns:xsi.
        // All other namespace and XML validation remains enabled.
        var nameTable = new NameTable();
        var namespaces = new XmlNamespaceManager(nameTable);
        namespaces.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");
        var context = new XmlParserContext(nameTable, namespaces, null, XmlSpace.None);

        using var reader = XmlReader.Create(stream, settings, context);
        reader.MoveToContent();

        if (reader.NodeType != XmlNodeType.Element ||
            reader.LocalName != "kml" ||
            reader.NamespaceURI != KmlNamespace)
        {
            throw new InvalidDataException("The file is not a supported KML 2.2 document.");
        }

        int placemarks = 0, points = 0, paths = 0, polygons = 0, folders = 0;

        // XmlReader advances through the document without constructing an XDocument.
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element ||
                reader.NamespaceURI != KmlNamespace)
                continue;

            switch (reader.LocalName)
            {
                case "Placemark": placemarks++; break;
                case "Point": points++; break;
                case "LineString": paths++; break;
                case "Polygon": polygons++; break;
                case "Folder": folders++; break;
            }
        }

        return new KmlAnalysisResult
        {
            PlacemarkCount = placemarks,
            PointCount = points,
            LineStringCount = paths,
            PolygonCount = polygons,
            FolderCount = folders
        };
    }
}

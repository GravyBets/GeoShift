
using System.Xml;
using System.Xml.Linq;
using GeoShift.Core.Models;

namespace GeoShift.Core.Services;

public sealed class KmlAnalyzer
{
    public KmlAnalysisResult Analyze(Stream stream)
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

        XNamespace kml = "http://www.opengis.net/kml/2.2";

        if (document.Root?.Name != kml + "kml")
        {
            throw new InvalidDataException(
                "The file is not a supported KML 2.2 document.");
        }

        return new KmlAnalysisResult
        {
            PlacemarkCount = document
                .Descendants(kml + "Placemark").Count(),

            PointCount = document
                .Descendants(kml + "Point").Count(),

            LineStringCount = document
                .Descendants(kml + "LineString").Count(),

            PolygonCount = document
                .Descendants(kml + "Polygon").Count(),

            FolderCount = document
                .Descendants(kml + "Folder").Count()
        };
    }
}


using System.Xml.Linq;
using GeoShift.Core.Services;

namespace GeoShift.Tests;

public class KmlAttributeReaderTests
{
    [Fact]
    public void ReadAttributes_ExtractsBothKmlFormats()
    {
        XNamespace kml =
            "http://www.opengis.net/kml/2.2";

        var placemark = XElement.Parse("""
            <Placemark xmlns="http://www.opengis.net/kml/2.2">
              <ExtendedData>
                <Data name="Site ID">
                  <value>TX-001</value>
                </Data>
                <Data name="Equipment Type">
                  <value>Radio</value>
                </Data>
                <SchemaData schemaUrl="#siteSchema">
                  <SimpleData name="Status">Active</SimpleData>
                </SchemaData>
              </ExtendedData>
            </Placemark>
            """);

        var attributes =
            KmlAttributeReader.ReadAttributes(placemark);

        Assert.Equal(3, attributes.Count);
        Assert.Equal("TX-001", attributes["Site ID"]);
        Assert.Equal("Radio", attributes["Equipment Type"]);
        Assert.Equal("Active", attributes["Status"]);
    }
}

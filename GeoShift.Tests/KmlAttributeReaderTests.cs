
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
    [Fact]
    public void ReadAttributes_IdenticalDuplicateValues_AreAccepted()
    {
        var placemark = XElement.Parse("""
            <Placemark xmlns="http://www.opengis.net/kml/2.2">
              <ExtendedData>
                <Data name="Site ID"><value>TX-001</value></Data>
                <SchemaData schemaUrl="#site">
                  <SimpleData name="site id">TX-001</SimpleData>
                </SchemaData>
              </ExtendedData>
            </Placemark>
            """);

        var attributes = KmlAttributeReader.ReadAttributes(placemark);

        Assert.Single(attributes);
        Assert.Equal("TX-001", attributes["Site ID"]);
    }

    [Fact]
    public void ReadAttributes_ConflictingDuplicateValues_Throws()
    {
        var placemark = XElement.Parse("""
            <Placemark xmlns="http://www.opengis.net/kml/2.2">
              <ExtendedData>
                <Data name="Site ID"><value>TX-001</value></Data>
                <SchemaData schemaUrl="#site">
                  <SimpleData name="site id">TX-999</SimpleData>
                </SchemaData>
              </ExtendedData>
            </Placemark>
            """);

        var error = Assert.Throws<InvalidDataException>(
            () => KmlAttributeReader.ReadAttributes(placemark));

        Assert.Contains("site id", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadAttributes_ConflictingRepeatedDataElements_Throws()
    {
        var placemark = XElement.Parse("""
            <Placemark xmlns="http://www.opengis.net/kml/2.2">
              <ExtendedData>
                <Data name="Serial Number"><value>SN-100</value></Data>
                <Data name="Serial Number"><value>SN-200</value></Data>
              </ExtendedData>
            </Placemark>
            """);

        Assert.Throws<InvalidDataException>(
            () => KmlAttributeReader.ReadAttributes(placemark));
    }


}

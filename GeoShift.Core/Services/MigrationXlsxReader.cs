using System.IO.Compression;
using System.Xml.Linq;

namespace GeoShift.Core.Services;

public static class MigrationXlsxReader
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Pr = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<IReadOnlyDictionary<string, string?>> Read(Stream input, int maxRows = 5000)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (maxRows < 1) throw new ArgumentOutOfRangeException(nameof(maxRows));
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, true);
        var book = Xml(zip, "xl/workbook.xml");
        var sheet = book.Descendants(Ns + "sheet").FirstOrDefault() ?? throw new InvalidDataException("No worksheet found.");
        var id = (string?)sheet.Attribute(R + "id");
        var rels = Xml(zip, "xl/_rels/workbook.xml.rels");
        var rel = rels.Descendants(Pr + "Relationship").FirstOrDefault(x => (string?)x.Attribute("Id") == id);
        if (rel is null || (string?)rel.Attribute("TargetMode") == "External") throw new InvalidDataException("Invalid worksheet relationship.");
        var target = (string?)rel.Attribute("Target") ?? throw new InvalidDataException("Worksheet path missing.");
        var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part == "..") { if (parts.Count == 0) throw new InvalidDataException("Invalid path."); parts.RemoveAt(parts.Count - 1); }
            else if (part != "." && part.Length > 0) parts.Add(part);
        }
        path = string.Join("/", parts);
        if (!path.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsupported worksheet path.");
        var shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml(zip, "xl/sharedStrings.xml")
            .Descendants(Ns + "si").Select(x => string.Concat(x.Descendants(Ns + "t").Select(t => t.Value))).ToList();
        var rows = Xml(zip, path).Descendants(Ns + "sheetData").Elements(Ns + "row").ToArray();
        if (rows.Length == 0) throw new InvalidDataException("Worksheet is empty.");
        if (rows.Length > maxRows + 1) throw new InvalidDataException("Too many Excel rows for preview.");
        var parsed = new List<Dictionary<int, string?>>();
        foreach (var row in rows)
        {
            var cells = new Dictionary<int, string?>();
            var next = 0;
            foreach (var cell in row.Elements(Ns + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                var index = reference is null ? next : Column(reference);
                next = index + 1;
                if (index >= 256) throw new InvalidDataException("Excel preview supports 256 columns.");
                if (cell.Element(Ns + "f") is not null) throw new InvalidDataException("Formula cells are not supported. Export calculated values to CSV.");
                var raw = (string?)cell.Element(Ns + "v");
                var type = (string?)cell.Attribute("t");
                string? value = type switch
                {
                    "s" when int.TryParse(raw, out var i) && i >= 0 && i < shared.Count => shared[i],
                    "s" => throw new InvalidDataException("Invalid shared string."),
                    "inlineStr" => string.Concat(cell.Descendants(Ns + "t").Select(x => x.Value)),
                    "b" => raw == "1" ? "TRUE" : "FALSE",
                    "e" => throw new InvalidDataException("Cell error found."),
                    _ => raw
                };
                if (!cells.TryAdd(index, value)) throw new InvalidDataException("Duplicate cell.");
            }
            parsed.Add(cells);
        }
        var headers = parsed[0].OrderBy(x => x.Key).ToArray();
        if (headers.Length == 0 || headers.Any(x => string.IsNullOrWhiteSpace(x.Value)) ||
            headers.Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new InvalidDataException("Headers must be nonempty and unique.");
        var output = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var row in parsed.Skip(1))
        {
            if (row.Keys.Any(x => !parsed[0].ContainsKey(x))) throw new InvalidDataException("Data found outside header columns.");
            var record = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers) record.Add(header.Value!, row.GetValueOrDefault(header.Key));
            output.Add(record);
        }
        return output;
    }

    private static int Column(string reference)
    {
        var value = 0;
        foreach (var ch in reference)
        {
            if (!char.IsLetter(ch)) break;
            if (ch is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z')) throw new InvalidDataException("Invalid cell address.");
            value = checked(value * 26 + char.ToUpperInvariant(ch) - 'A' + 1);
        }
        if (value == 0) throw new InvalidDataException("Invalid cell address.");
        return value - 1;
    }

    private static XDocument Xml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException("Missing Excel component: " + path);
        if (entry.Length > 16 * 1024 * 1024) throw new InvalidDataException("Excel component too large.");
        using var stream = entry.Open();
        using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 16 * 1024 * 1024
        });
        return XDocument.Load(reader);
    }
}

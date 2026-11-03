using System.Text;

namespace GeoShift.Core.Services;

/// <summary>Small CSV importer for interactive previews. Enforce upload limits at the caller.</summary>
public static class MigrationCsvReader
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string?>> Read(TextReader input, int maxRows = 5000)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (maxRows < 1) throw new ArgumentOutOfRangeException(nameof(maxRows));
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var atStart = true;
        while (true)
        {
            var next = input.Read();
            if (next < 0)
            {
                if (quoted) throw new InvalidDataException("Unterminated quoted CSV field.");
                if (!atStart || fields.Count > 0)
                {
                    fields.Add(field.ToString());
                    records.Add(fields.ToArray());
                }
                if (records.Count > maxRows + 1)
                    throw new InvalidDataException($"CSV preview is limited to {maxRows} data rows.");
                break;
            }
            var ch = (char)next;
            if (quoted)
            {
                if (ch == '"')
                {
                    if (input.Peek() == '"') { input.Read(); field.Append('"'); }
                    else quoted = false;
                }
                else field.Append(ch);
                continue;
            }
            if (ch == '"' && atStart) { quoted = true; atStart = false; }
            else if (ch == ',') { fields.Add(field.ToString()); field.Clear(); atStart = true; }
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && input.Peek() == '\n') input.Read();
                fields.Add(field.ToString());
                records.Add(fields.ToArray());
                if (records.Count > maxRows + 1)
                    throw new InvalidDataException($"CSV preview is limited to {maxRows} data rows.");
                fields = new List<string>();
                field.Clear();
                atStart = true;
            }
            else if (ch == '"') throw new InvalidDataException("Unexpected quote in CSV field.");
            else { field.Append(ch); atStart = false; }
        }
        if (records.Count == 0) throw new InvalidDataException("CSV is empty.");
        var headers = records[0];
        if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace) ||
            headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new InvalidDataException("CSV headers must be nonempty and unique.");
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var record in records.Skip(1))
        {
            if (record.Length != headers.Length)
                throw new InvalidDataException("CSV record has a different number of columns than the header.");
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++) row.Add(headers[i], record[i]);
            rows.Add(row);
        }
        return rows;
    }
}

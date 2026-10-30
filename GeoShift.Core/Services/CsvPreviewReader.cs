using System.Text;

namespace GeoShift.Core.Services;

/// <summary>Reads a bounded number of RFC 4180-style CSV records for display.</summary>
public static class CsvPreviewReader
{
    public static IReadOnlyList<IReadOnlyList<string>> Read(
        string csv, int maxRecords = 11)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRecords);

        var records = new List<IReadOnlyList<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        bool atFieldStart = true;

        for (int i = 0; i < csv.Length; i++)
        {
            char ch = csv[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            if (ch == '"' && atFieldStart)
            {
                quoted = true;
                atFieldStart = false;
                continue;
            }

            if (ch == ',')
            {
                record.Add(field.ToString());
                field.Clear();
                atFieldStart = true;
                continue;
            }

            if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    i++;

                record.Add(field.ToString());
                records.Add(record);
                if (records.Count >= maxRecords)
                    return records;

                record = new List<string>();
                field.Clear();
                atFieldStart = true;
                continue;
            }

            field.Append(ch);
            atFieldStart = false;
        }

        if (quoted)
            throw new InvalidDataException("The CSV contains an unterminated quoted field.");

        if (record.Count > 0 || field.Length > 0 || !atFieldStart)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }
}

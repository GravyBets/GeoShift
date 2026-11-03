namespace GeoShift.Web.Components.Pages;

/// <summary>Reads only the CSV header and first N records, including quoted newlines.</summary>
internal static class GeoShiftCsvPreview
{
    public static IReadOnlyList<IReadOnlyList<string>> ReadFirstRows(TextReader reader, int records)
    {
        var rows = new List<IReadOnlyList<string>>();
        for (var rowIndex = 0; rowIndex <= records; rowIndex++)
        {
            var cells = new List<string>();
            var field = new System.Text.StringBuilder();
            var quoted = false;
            var any = false;
            while (true)
            {
                var code = reader.Read();
                if (code == -1)
                {
                    if (quoted) throw new InvalidDataException("Unterminated quoted CSV field.");
                    if (any || cells.Count > 0)
                    {
                        cells.Add(field.ToString());
                        rows.Add(cells);
                    }
                    return rows;
                }
                var ch = (char)code;
                any = true;
                if (ch == '"')
                {
                    if (quoted && reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else quoted = !quoted;
                }
                else if (!quoted && ch == ',')
                {
                    cells.Add(field.ToString());
                    field.Clear();
                }
                else if (!quoted && (ch == '\n' || ch == '\r'))
                {
                    if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                    cells.Add(field.ToString());
                    rows.Add(cells);
                    break;
                }
                else field.Append(ch);
            }
        }
        return rows;
    }
}

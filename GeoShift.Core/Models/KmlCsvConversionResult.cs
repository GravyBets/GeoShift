
namespace GeoShift.Core.Models;

public sealed class KmlCsvConversionResult
{
    public string CsvContent { get; init; } = string.Empty;

    public int ExportedPointCount { get; init; }

    public int SkippedPointCount { get; init; }

    public int ExcludedLineStringCount { get; init; }

    public int ExcludedPolygonCount { get; init; }

    public int CustomColumnCount { get; init; }

    public bool HasWarnings =>
        SkippedPointCount > 0 ||
        ExcludedLineStringCount > 0 ||
        ExcludedPolygonCount > 0;
}


namespace GeoShift.Core.Models;

public sealed class KmlAnalysisResult
{
    public int PlacemarkCount { get; set; }
    public int PointCount { get; set; }
    public int LineStringCount { get; set; }
    public int PolygonCount { get; set; }
    public int FolderCount { get; set; }
}

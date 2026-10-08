
using System.IO.Compression;
using GeoShift.Core.Models;

namespace GeoShift.Core.Services;

public sealed class KmzAnalyzer
{
    private const long MaxArchiveBytes = 50 * 1024 * 1024;
    private const long MaxKmlBytes = 20 * 1024 * 1024;

    public KmlAnalysisResult Analyze(Stream stream)
    {
        using var kmlStream = ExtractKml(stream);
        return new KmlAnalyzer().Analyze(kmlStream);
    }

    public string ConvertToCsv(Stream stream)
    {
        return ConvertToCsvWithSummary(stream).CsvContent;
    }

    public KmlCsvConversionResult ConvertToCsvWithSummary(Stream stream)
    {
        using var kmlStream = ExtractKml(stream);

        return new KmlCsvConverter()
            .ConvertWithSummary(kmlStream);
    }


    private static MemoryStream ExtractKml(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new InvalidDataException(
                "The KMZ stream must be readable and seekable.");
        }

        if (stream.Length > MaxArchiveBytes)
        {
            throw new InvalidDataException(
                "The KMZ file exceeds the 50 MB limit.");
        }

        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Read,
            leaveOpen: true);

        var kmlEntry = archive.Entries
            .FirstOrDefault(e =>
                e.FullName.Equals(
                    "doc.kml",
                    StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith(
                    ".kml",
                    StringComparison.OrdinalIgnoreCase));

        if (kmlEntry is null)
        {
            throw new InvalidDataException(
                "The KMZ archive does not contain a KML file.");
        }

        if (kmlEntry.Length > MaxKmlBytes)
        {
            throw new InvalidDataException(
                "The KML document exceeds the 20 MB limit.");
        }

        using var entryStream = kmlEntry.Open();
        var memory = new MemoryStream();

        try
        {
            var buffer = new byte[81920];
            long totalBytes = 0;
            int bytesRead;

            while ((bytesRead = entryStream.Read(
                       buffer, 0, buffer.Length)) > 0)
            {
                totalBytes += bytesRead;

                if (totalBytes > MaxKmlBytes)
                {
                    throw new InvalidDataException(
                        "The decompressed KML exceeds the 20 MB limit.");
                }

                memory.Write(buffer, 0, bytesRead);
            }

            memory.Position = 0;
            return memory;
        }
        catch
        {
            memory.Dispose();
            throw;
        }
    }
}

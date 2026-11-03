
using System.IO.Compression;
using GeoShift.Core.Models;

namespace GeoShift.Core.Services;

public sealed class KmzAnalyzer
{
    private const long MaxArchiveBytes = 50 * 1024 * 1024;
    private const long MaxKmlBytes = 200 * 1024 * 1024;

    public KmlAnalysisResult Analyze(Stream stream)
    {
        using var archive = OpenArchive(stream);
        using var kmlStream = OpenKmlEntry(archive);
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


    /// <summary>
    /// Streams a KMZ's KML content into a bounded temporary file so the
    /// two-pass CSV converter can seek without holding the KML in memory.
    /// </summary>
    public KmlCsvConversionResult ConvertToCsvStreaming(Stream stream, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var temporaryPath = Path.GetTempFileName();
        try
        {
            using var temporary = new FileStream(
                temporaryPath, FileMode.Open, FileAccess.ReadWrite,
                FileShare.None, 81920, FileOptions.SequentialScan);

            using (var archive = OpenArchive(stream))
            using (var kmlStream = OpenKmlEntry(archive))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = kmlStream.Read(buffer, 0, buffer.Length)) > 0)
                    temporary.Write(buffer, 0, read);
            }

            temporary.Position = 0;
            return new StreamingKmlCsvConverter().Convert(temporary, output);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static ZipArchive OpenArchive(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
            throw new InvalidDataException("The KMZ stream must be readable and seekable.");
        if (stream.Length > MaxArchiveBytes)
            throw new InvalidDataException("The KMZ file exceeds the 50 MB compressed limit.");
        return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
    }

    private static ZipArchiveEntry GetKmlEntry(ZipArchive archive)
    {
        var entry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Equals("doc.kml", StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            throw new InvalidDataException("The KMZ archive does not contain a KML file.");
        return entry;
    }

    private static Stream OpenKmlEntry(ZipArchive archive)
    {
        var entry = GetKmlEntry(archive);
        if (entry.Length > MaxKmlBytes)
            throw new InvalidDataException("The extracted KML exceeds the 200 MB limit.");
        return new LimitedReadStream(entry.Open(), MaxKmlBytes);
    }

    // CSV still uses the existing converter and its conservative 20 MB cap.
    private static MemoryStream ExtractKml(Stream stream)
    {
        using var archive = OpenArchive(stream);
        var entry = GetKmlEntry(archive);
        const long conversionLimit = 20 * 1024 * 1024;
        if (entry.Length > conversionLimit)
            throw new InvalidDataException("CSV conversion currently supports extracted KML up to 20 MB. Large-file CSV streaming is coming next.");

        using var entryStream = entry.Open();
        var memory = new MemoryStream();
        try
        {
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > conversionLimit)
                    throw new InvalidDataException("CSV conversion currently supports extracted KML up to 20 MB. Large-file CSV streaming is coming next.");
                memory.Write(buffer, 0, read);
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

    private sealed class LimitedReadStream(Stream inner, long limit) : Stream
    {
        private long _read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty) return 0;
            var allowed = (int)Math.Min(buffer.Length, limit - _read + 1);
            var read = inner.Read(buffer[..allowed]);
            _read += read;
            if (_read > limit)
                throw new InvalidDataException("The extracted KML exceeds the 200 MB limit.");
            return read;
        }
        public override int ReadByte()
        {
            Span<byte> buffer = stackalloc byte[1];
            return Read(buffer) == 0 ? -1 : buffer[0];
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing: disposing);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace PhotoUpload.Web.Services;

public class ThumbnailService : IThumbnailService
{
    private const int ThumbnailWidth  = 400;
    private const int PreviewWidth    = 1920;
    private const int MinPreviewWidth = 800; // reject extracted JPEGs smaller than this

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tiff", ".tif",
        ".cr2", ".nef", ".arw"
    };

    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".nef", ".arw"
    };

    private readonly IFileStorageService _storage;
    private readonly ILogger<ThumbnailService> _logger;

    public ThumbnailService(IFileStorageService storage, ILogger<ThumbnailService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public bool SupportsFormat(string extension) => SupportedExtensions.Contains(extension);

    // ── Public API ─────────────────────────────────────────────────────────────

    public async Task<string?> GenerateThumbnailAsync(
        string originalRelativePath, string galleryToken, string fileName, bool overwrite = false)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext)) return null;

        try
        {
            var sourcePath = _storage.GetAbsolutePath(originalRelativePath);
            var thumbRelative = $"{galleryToken}/thumbs/thumb_{Path.GetFileNameWithoutExtension(fileName)}.jpg";
            var thumbAbsolute = _storage.GetAbsolutePath(thumbRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(thumbAbsolute)!);

            // Another concurrent request may have already written this file
            if (!overwrite && File.Exists(thumbAbsolute)) return thumbRelative;

            using var image = await LoadImageAsync(sourcePath, ext);
            if (image == null) return null;

            // Phones/cameras store pixels as captured plus an EXIF tag saying how to
            // rotate for display. Apply that now so the thumbnail is physically upright.
            image.Mutate(x => x.AutoOrient());

            int h = (int)((double)image.Height / image.Width * ThumbnailWidth);
            image.Mutate(x => x.Resize(ThumbnailWidth, h));
            try
            {
                await image.SaveAsJpegAsync(thumbAbsolute, new JpegEncoder { Quality = 80 });
            }
            catch (IOException) when (File.Exists(thumbAbsolute))
            {
                // Race: another request finished first — file is fine, use it
            }
            return thumbRelative;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate thumbnail for {File}", fileName);
            return null;
        }
    }

    public async Task<string?> GeneratePreviewAsync(
        string originalRelativePath, string galleryToken, string fileName, bool overwrite = false)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext)) return null;

        try
        {
            var sourcePath = _storage.GetAbsolutePath(originalRelativePath);
            var previewRelative = $"{galleryToken}/thumbs/preview_{Path.GetFileNameWithoutExtension(fileName)}.jpg";
            var previewAbsolute = _storage.GetAbsolutePath(previewRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(previewAbsolute)!);

            // Another concurrent request may have already written this file
            if (!overwrite && File.Exists(previewAbsolute)) return previewRelative;

            if (RawExtensions.Contains(ext))
            {
                // RAW: extract embedded JPEG candidates, try each until one decodes.
                // The embedded JPEG itself carries no orientation tag of its own — the
                // camera only records it once, on the RAW file's own IFD0 — so it has
                // to be read separately and applied to the decoded candidate.
                var orientation = ReadRawOrientation(sourcePath);
                foreach (var jpegBytes in ExtractJpegCandidates(sourcePath))
                {
                    try
                    {
                        using var ms = new MemoryStream(jpegBytes);
                        var info = await Image.IdentifyAsync(ms);
                        if (info == null) continue;

                        // Skip tiny embedded thumbnails — they look terrible when stretched
                        if (Math.Max(info.Width, info.Height) < MinPreviewWidth) continue;

                        try
                        {
                            ms.Position = 0;
                            using var image = await Image.LoadAsync(ms);
                            // Apply the EXIF orientation before sizing decisions — rotation
                            // can swap width/height, and the raw candidate bytes are never
                            // physically rotated on their own.
                            ApplyOrientationTag(image, orientation);
                            image.Mutate(x => x.AutoOrient());
                            if (image.Width > PreviewWidth)
                            {
                                int h = (int)((double)image.Height / image.Width * PreviewWidth);
                                image.Mutate(x => x.Resize(PreviewWidth, h));
                            }
                            await image.SaveAsJpegAsync(previewAbsolute, new JpegEncoder { Quality = 92 });
                        }
                        catch (IOException) when (File.Exists(previewAbsolute)) { /* race — file written by other request */ }
                        return previewRelative;
                    }
                    catch (Exception ex) when (IsDecodeError(ex)) { /* try next candidate */ }
                }
                _logger.LogWarning("No decodable JPEG found for preview in {File}", fileName);
                return null;
            }
            else
            {
                // Regular image: decode, auto-orient, and resize to preview width if needed
                using var image = await Image.LoadAsync(sourcePath);
                image.Mutate(x => x.AutoOrient());
                if (image.Width > PreviewWidth)
                {
                    int h = (int)((double)image.Height / image.Width * PreviewWidth);
                    image.Mutate(x => x.Resize(PreviewWidth, h));
                }
                try
                {
                    await image.SaveAsJpegAsync(previewAbsolute, new JpegEncoder { Quality = 92 });
                }
                catch (IOException) when (File.Exists(previewAbsolute)) { /* race — file written by other request */ }
                return previewRelative;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate preview for {File}", fileName);
            return null;
        }
    }

    // ── Image loading ──────────────────────────────────────────────────────────

    private async Task<Image?> LoadImageAsync(string sourcePath, string ext)
    {
        if (!RawExtensions.Contains(ext)) return await Image.LoadAsync(sourcePath);

        var orientation = ReadRawOrientation(sourcePath);
        foreach (var jpegData in ExtractJpegCandidates(sourcePath))
        {
            try
            {
                using var ms = new MemoryStream(jpegData);
                var image = await Image.LoadAsync(ms);
                ApplyOrientationTag(image, orientation);
                return image;
            }
            catch (Exception ex) when (IsDecodeError(ex)) { /* try next candidate */ }
        }

        _logger.LogWarning("No decodable JPEG found in RAW file {Path}", sourcePath);
        return null;
    }

    private static bool IsDecodeError(Exception ex) =>
        ex is InvalidImageContentException or UnknownImageFormatException;

    private static void ApplyOrientationTag(Image image, ushort orientation)
    {
        if (orientation <= 1) return; // already normal, or none found
        image.Metadata.ExifProfile ??= new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, orientation);
    }

    /// <summary>
    /// Reads the EXIF Orientation tag (0x0112) straight from the RAW file's own IFD0.
    /// The embedded preview/thumbnail JPEGs extracted from RAW files are raw sensor-order
    /// bitmaps with no orientation metadata of their own — the camera records it once, on
    /// the RAW file's main image directory, and every embedded JPEG relies on that same
    /// value for correct display.
    /// </summary>
    private static ushort ReadRawOrientation(string rawFilePath)
    {
        try
        {
            using var fs = new FileStream(rawFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < 8) return 1;
            fs.Position = 0;

            int b0 = fs.ReadByte(), b1 = fs.ReadByte();
            bool le = b0 == 'I' && b1 == 'I';
            if (!le && !(b0 == 'M' && b1 == 'M')) return 1;

            using var r = new BinaryReader(fs, System.Text.Encoding.ASCII, leaveOpen: true);
            ushort magic = ReadU16(r, le);
            if (magic != 42 && magic != 0x4352 && magic != 0x4F52) return 1;

            uint firstIfd = ReadU32(r, le);
            if (firstIfd == 0 || firstIfd + 2 > (uint)fs.Length) return 1;

            fs.Position = firstIfd;
            ushort count = ReadU16(r, le);

            for (int i = 0; i < count && fs.Position + 12 <= fs.Length; i++)
            {
                ushort tag = ReadU16(r, le);
                ReadU16(r, le); // type
                ReadU32(r, le); // count
                uint val = ReadU32(r, le);

                if (tag == 0x0112) // Orientation — SHORT, left-justified in the 4-byte value field
                    return le ? (ushort)(val & 0xFFFF) : (ushort)(val >> 16);
            }
        }
        catch { }
        return 1;
    }

    // ── JPEG extraction ────────────────────────────────────────────────────────
    //
    // Yields candidates in decode-preference order:
    //   1. JPEGInterchangeFormat entries (0x0201/0x0202) — always standard 8-bit JPEG
    //   2. StripOffsets entries (0x0111/0x0117)          — may be lossless/proprietary (NEF RAW)
    //   3. Byte-scan results                             — catch-all
    //
    // Within each group: largest first (higher resolution = better quality).
    // Callers skip candidates that ImageSharp rejects (e.g. NEF's lossless 14-bit RAW blobs).

    private static IEnumerable<byte[]> ExtractJpegCandidates(string rawFilePath)
    {
        var jpegIF = new List<byte[]>();
        var strips = new List<byte[]>();
        var scan   = new List<byte[]>();

        try
        {
            using var fs = new FileStream(rawFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ExtractViaTiff(fs, jpegIF, strips);
            ScanForJpegs(fs, scan);
        }
        catch { }

        var seen = new HashSet<int>();

        foreach (var j in jpegIF.OrderByDescending(b => b.Length))
            if (seen.Add(j.Length)) yield return j;

        foreach (var j in strips.OrderByDescending(b => b.Length))
            if (seen.Add(j.Length)) yield return j;

        foreach (var j in scan.OrderByDescending(b => b.Length))
            if (seen.Add(j.Length)) yield return j;
    }

    // ── TIFF IFD walker ────────────────────────────────────────────────────────

    private static void ExtractViaTiff(FileStream fs, List<byte[]> jpegIF, List<byte[]> strips)
    {
        if (fs.Length < 8) return;
        fs.Position = 0;

        int b0 = fs.ReadByte(), b1 = fs.ReadByte();
        bool le = b0 == 'I' && b1 == 'I';
        if (!le && !(b0 == 'M' && b1 == 'M')) return;

        using var r = new BinaryReader(fs, System.Text.Encoding.ASCII, leaveOpen: true);
        ushort magic = ReadU16(r, le);
        if (magic != 42 && magic != 0x4352 && magic != 0x4F52) return; // TIFF / CR2 / ORF

        uint firstIfd = ReadU32(r, le);
        var visited = new HashSet<uint>();

        void TryAdd(List<byte[]> list, uint off, uint len)
        {
            if (off == 0 || len < 4 || off + len > (uint)fs.Length) return;
            fs.Position = off;
            var data = r.ReadBytes((int)len);
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8)
                list.Add(data);
        }

        void Walk(uint offset)
        {
            if (offset == 0 || offset >= (uint)fs.Length || !visited.Add(offset)) return;
            try
            {
                fs.Position = offset;
                ushort count = ReadU16(r, le);

                uint jpegOff = 0, jpegLen = 0, stripOff = 0, stripLen = 0;
                var subs = new List<uint>();

                for (int i = 0; i < count && fs.Position + 12 <= fs.Length; i++)
                {
                    ushort tag = ReadU16(r, le);
                    ReadU16(r, le); // type
                    ReadU32(r, le); // count
                    uint val = ReadU32(r, le);

                    switch (tag)
                    {
                        case 0x0111: stripOff = val; break; // StripOffsets
                        case 0x0117: stripLen = val; break; // StripByteCounts
                        case 0x0201: jpegOff  = val; break; // JPEGInterchangeFormat
                        case 0x0202: jpegLen  = val; break; // JPEGInterchangeFormatLength
                        case 0x014A: case 0x8769: case 0xA005: subs.Add(val); break;
                    }
                }

                uint next = fs.Position + 4 <= fs.Length ? ReadU32(r, le) : 0;

                TryAdd(jpegIF, jpegOff, jpegLen);   // standard preview — always safe
                TryAdd(strips, stripOff, stripLen); // may be lossless RAW — try last

                foreach (var s in subs) Walk(s);
                Walk(next);
            }
            catch { }
        }

        Walk(firstIfd);
    }

    // ── Byte scanner ───────────────────────────────────────────────────────────

    private static void ScanForJpegs(FileStream fs, List<byte[]> results)
    {
        const long Limit = 50L * 1024 * 1024;
        fs.Position = 0;
        int len = (int)Math.Min(fs.Length, Limit);
        var data = new byte[len];
        _ = fs.Read(data, 0, len);

        var sois = new List<int>();
        for (int i = 0; i < data.Length - 1; i++)
            if (data[i] == 0xFF && data[i + 1] == 0xD8)
                sois.Add(i);

        for (int si = 0; si < sois.Count; si++)
        {
            int start = sois[si];
            int searchEnd = si + 1 < sois.Count ? sois[si + 1] : data.Length;

            int eoi = -1;
            for (int j = searchEnd - 2; j > start + 1; j--)
                if (data[j] == 0xFF && data[j + 1] == 0xD9) { eoi = j + 2; break; }

            if (eoi > start + 4)
                results.Add(data[start..eoi]);
        }
    }

    // ── Endianness helpers ─────────────────────────────────────────────────────

    private static ushort ReadU16(BinaryReader r, bool le)
    {
        var b = r.ReadBytes(2);
        return le ? (ushort)(b[0] | (b[1] << 8)) : (ushort)((b[0] << 8) | b[1]);
    }

    private static uint ReadU32(BinaryReader r, bool le)
    {
        var b = r.ReadBytes(4);
        return le
            ? (uint)(b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24))
            : (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }
}

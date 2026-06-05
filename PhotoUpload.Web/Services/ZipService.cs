using System.IO.Compression;

namespace PhotoUpload.Web.Services;

public class ZipService : IZipService
{
    public async Task<Stream> CreateZipAsync(IEnumerable<(string FilePath, string EntryName)> files)
    {
        var memStream = new MemoryStream();
        using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (filePath, entryName) in files)
            {
                if (!File.Exists(filePath)) continue;
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await fileStream.CopyToAsync(entryStream);
            }
        }
        memStream.Position = 0;
        return memStream;
    }
}

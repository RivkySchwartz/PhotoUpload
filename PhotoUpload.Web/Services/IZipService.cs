namespace PhotoUpload.Web.Services;

public interface IZipService
{
    /// <summary>Creates a ZIP stream containing the given files. Caller is responsible for disposing.</summary>
    Task<Stream> CreateZipAsync(IEnumerable<(string FilePath, string EntryName)> files);
}

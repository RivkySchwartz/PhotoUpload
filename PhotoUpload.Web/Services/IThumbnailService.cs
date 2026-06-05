namespace PhotoUpload.Web.Services;

public interface IThumbnailService
{
    Task<string?> GenerateThumbnailAsync(string originalRelativePath, string galleryToken, string fileName);

    /// <summary>
    /// For RAW files only: generates a full-quality JPEG preview (up to 1920 px wide)
    /// suitable for the lightbox. Returns null for non-RAW formats.
    /// </summary>
    Task<string?> GeneratePreviewAsync(string originalRelativePath, string galleryToken, string fileName);

    bool SupportsFormat(string extension);
}

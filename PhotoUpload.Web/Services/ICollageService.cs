namespace PhotoUpload.Web.Services;

public interface ICollageService
{
    /// <summary>
    /// Generates a collage from the provided image paths and returns the relative path
    /// to the saved collage file, or null if the paths list is empty.
    /// </summary>
    Task<string?> GenerateCollageAsync(IReadOnlyList<string> imageAbsolutePaths, string galleryToken);
}

namespace PhotoUpload.Web.Services;

public interface IFileStorageService
{
    /// <summary>Saves an uploaded file and returns its relative storage path.</summary>
    Task<string> SaveFileAsync(Stream stream, string galleryToken, string fileName);

    /// <summary>Deletes a file at the given relative path.</summary>
    Task DeleteFileAsync(string relativePath);

    /// <summary>Deletes all files for a gallery directory.</summary>
    Task DeleteGalleryFilesAsync(string galleryToken);

    /// <summary>Returns the absolute path for a relative storage path.</summary>
    string GetAbsolutePath(string relativePath);

    /// <summary>Returns the public URL for a relative storage path.</summary>
    string GetPublicUrl(string relativePath);
}

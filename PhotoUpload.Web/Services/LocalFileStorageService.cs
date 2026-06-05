namespace PhotoUpload.Web.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageRoot;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalFileStorageService(IConfiguration config, IHttpContextAccessor httpContextAccessor)
    {
        _storageRoot = config["Storage:RootPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _httpContextAccessor = httpContextAccessor;
        Directory.CreateDirectory(_storageRoot);
    }

    public async Task<string> SaveFileAsync(Stream stream, string galleryToken, string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        var galleryDir = Path.Combine(_storageRoot, galleryToken);
        Directory.CreateDirectory(galleryDir);

        var relativePath = Path.Combine(galleryToken, safeFileName);
        var absolutePath = Path.Combine(_storageRoot, relativePath);

        // Ensure unique filename
        var ext = Path.GetExtension(safeFileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(safeFileName);
        var counter = 1;
        while (File.Exists(absolutePath))
        {
            safeFileName = $"{nameWithoutExt}_{counter++}{ext}";
            relativePath = Path.Combine(galleryToken, safeFileName);
            absolutePath = Path.Combine(_storageRoot, relativePath);
        }

        using var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream);

        return relativePath.Replace('\\', '/');
    }

    public Task DeleteFileAsync(string relativePath)
    {
        var absolute = GetAbsolutePath(relativePath);
        if (File.Exists(absolute))
            File.Delete(absolute);
        return Task.CompletedTask;
    }

    public Task DeleteGalleryFilesAsync(string galleryToken)
    {
        var dir = Path.Combine(_storageRoot, galleryToken);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }

    public string GetAbsolutePath(string relativePath)
        => Path.Combine(_storageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public string GetPublicUrl(string relativePath)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            var baseUrl = $"{request.Scheme}://{request.Host}";
            return $"{baseUrl}/uploads/{relativePath.Replace('\\', '/')}";
        }
        return $"/uploads/{relativePath.Replace('\\', '/')}";
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoUpload.Data;
using PhotoUpload.Data.Models;
using PhotoUpload.Web.DTOs;
using PhotoUpload.Web.Services;

namespace PhotoUpload.Web.Controllers;

/// <summary>Admin-protected gallery management endpoints.</summary>
[ApiController]
[Route("api/galleries")]
[Authorize]
public class GalleriesController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".cr2", ".nef", ".arw", ".webp", ".tiff", ".tif"
    };


    private readonly photoDataContext _db;
    private readonly IFileStorageService _storage;
    private readonly IThumbnailService _thumbs;
    private readonly IZipService _zip;
    private readonly ILogger<GalleriesController> _logger;

    public GalleriesController(
        photoDataContext db,
        IFileStorageService storage,
        IThumbnailService thumbs,
        IZipService zip,
        ILogger<GalleriesController> logger)
    {
        _db = db;
        _storage = storage;
        _thumbs = thumbs;
        _zip = zip;
        _logger = logger;
    }

    // ── List all galleries ─────────────────────────────────────────────────

    /// <summary>Returns all galleries with summary counts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GalleryDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var galleries = await _db.Galleries
            .Include(g => g.Photos)
            .Include(g => g.Selections)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return Ok(galleries.Select(MapToDto));
    }

    // ── Get single gallery ────────────────────────────────────────────────

    /// <summary>Returns a single gallery by ID.</summary>
    /// <param name="id">Gallery database ID.</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GalleryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var gallery = await _db.Galleries
            .Include(g => g.Photos)
            .Include(g => g.Selections)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (gallery == null) return NotFound();
        return Ok(MapToDto(gallery));
    }

    // ── Create gallery ────────────────────────────────────────────────────

    /// <summary>Creates a new gallery and returns it with its shareable link token.</summary>
    /// <param name="request">Gallery creation parameters.</param>
    [HttpPost]
    [Authorize(Roles = "Admin,Photographer")]
    [ProducesResponseType(typeof(GalleryDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateGalleryRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var gallery = new Gallery
        {
            Name = request.Name.Trim(),
            ClientName = request.ClientName.Trim(),
            ClientEmail = request.ClientEmail?.Trim(),
            MaxSelections = request.MaxSelections,
            UniqueToken = Guid.NewGuid().ToString("N"),
            PasswordHash = string.IsNullOrWhiteSpace(request.Password)
                ? null
                : BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Galleries.Add(gallery);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Gallery created: {Id} - {Name}", gallery.Id, gallery.Name);
        return CreatedAtAction(nameof(GetById), new { id = gallery.Id }, MapToDto(gallery));
    }

    // ── Delete gallery ────────────────────────────────────────────────────

    /// <summary>Deletes a gallery and all its photos.</summary>
    /// <param name="id">Gallery database ID.</param>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var gallery = await _db.Galleries
            .Include(g => g.Selections)
            .Include(g => g.Photos)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (gallery == null) return NotFound();

        _db.Selections.RemoveRange(gallery.Selections);
        _db.Photos.RemoveRange(gallery.Photos);
        _db.Galleries.Remove(gallery);
        await _db.SaveChangesAsync();

        await _storage.DeleteGalleryFilesAsync(gallery.UniqueToken);
        _logger.LogInformation("Gallery deleted: {Id}", id);
        return NoContent();
    }

    // ── Upload photos ─────────────────────────────────────────────────────

    /// <summary>Uploads one or more photos to a gallery. Supports chunked/multipart upload.</summary>
    /// <param name="id">Gallery database ID.</param>
    [HttpPost("{id:int}/upload")]
    [Authorize(Roles = "Admin,Photographer")]
    [RequestSizeLimit(2_000_000_000)]
    [ProducesResponseType(typeof(IEnumerable<PhotoDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Upload(int id, [FromForm] IFormFileCollection files)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery == null) return NotFound();

        if (files == null || files.Count == 0)
            return BadRequest(new { message = "No files provided." });

        var maxOrder = await _db.Photos
            .Where(p => p.GalleryId == id)
            .Select(p => (int?)p.SortOrder)
            .MaxAsync() ?? 0;

        // Filter to allowed extensions first
        var validFiles = files
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f.FileName).ToLowerInvariant()))
            .ToList();
        var rejectedCount = files.Count - validFiles.Count;
        if (rejectedCount > 0)
            _logger.LogWarning("Rejected {Count} file(s) with unsupported extensions", rejectedCount);

        // Save files to disk only — preview generation is a separate call
        var savedPaths = await Task.WhenAll(validFiles.Select(async file =>
        {
            await using var stream = file.OpenReadStream();
            var relativePath = await _storage.SaveFileAsync(stream, gallery.UniqueToken, file.FileName);
            return relativePath;
        }));

        var newPhotos = new List<Photo>();
        foreach (var relativePath in savedPaths)
        {
            var photo = new Photo
            {
                GalleryId = id,
                FileName = Path.GetFileName(relativePath),
                OriginalPath = relativePath,
                SortOrder = ++maxOrder
            };
            _db.Photos.Add(photo);
            newPhotos.Add(photo);
        }

        await _db.SaveChangesAsync();

        return Ok(newPhotos.Select(MapPhotoToDto));
    }

    // ── Get photos ────────────────────────────────────────────────────────

    /// <summary>Returns all photos in a gallery with thumbnail and original URLs.</summary>
    /// <param name="id">Gallery database ID.</param>
    [HttpGet("{id:int}/photos")]
    [ProducesResponseType(typeof(IEnumerable<PhotoDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPhotos(int id)
    {
        if (!await _db.Galleries.AnyAsync(g => g.Id == id)) return NotFound();

        var photos = await _db.Photos
            .Where(p => p.GalleryId == id)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        return Ok(photos.Select(MapPhotoToDto));
    }

    // ── Delete single photo ───────────────────────────────────────────────

    /// <summary>Deletes a single photo from a gallery.</summary>
    /// <param name="id">Gallery database ID.</param>
    /// <param name="photoId">Photo database ID.</param>
    [HttpDelete("{id:int}/photos/{photoId:int}")]
    [Authorize(Roles = "Admin,Photographer")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeletePhoto(int id, int photoId)
    {
        var photo = await _db.Photos
            .Include(p => p.Selections)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.GalleryId == id);

        if (photo == null) return NotFound();

        _db.Selections.RemoveRange(photo.Selections);
        _db.Photos.Remove(photo);
        await _db.SaveChangesAsync();

        await _storage.DeleteFileAsync(photo.OriginalPath);
        if (photo.ThumbnailPath != null)
            await _storage.DeleteFileAsync(photo.ThumbnailPath);

        return NoContent();
    }

    // ── Get selections ────────────────────────────────────────────────────

    /// <summary>Returns the photos a client has selected for a gallery.</summary>
    /// <param name="id">Gallery database ID.</param>
    [HttpGet("{id:int}/selections")]
    [ProducesResponseType(typeof(IEnumerable<SelectionDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSelections(int id)
    {
        if (!await _db.Galleries.AnyAsync(g => g.Id == id)) return NotFound();

        var selections = await _db.Selections
            .Include(s => s.Photo)
            .Where(s => s.GalleryId == id)
            .OrderBy(s => s.Photo.SortOrder)
            .ToListAsync();

        return Ok(selections.Select(s => new SelectionDto(
            s.Id,
            s.PhotoId,
            s.Photo.FileName,
            GetThumbnailUrl(s.Photo),
            s.SelectedAt
        )));
    }

    // ── Get print orders ──────────────────────────────────────────────────

    [HttpGet("{id:int}/print-orders")]
    [ProducesResponseType(typeof(IEnumerable<PrintOrderDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPrintOrders(int id)
    {
        if (!await _db.Galleries.AnyAsync(g => g.Id == id)) return NotFound();

        var orders = await _db.PrintOrders
            .Include(p => p.Photo)
            .Where(p => p.GalleryId == id)
            .OrderBy(p => p.PhotoId).ThenBy(p => p.Size)
            .ToListAsync();

        return Ok(orders.Select(o => new PrintOrderDto(
            o.Id, o.PhotoId, o.Photo.FileName,
            GetThumbnailUrl(o.Photo), o.Size, o.Quantity
        )));
    }

    // ── Download selected photos as ZIP ───────────────────────────────────

    /// <summary>Streams a ZIP archive of all client-selected photos.</summary>
    /// <param name="id">Gallery database ID.</param>
    [HttpGet("{id:int}/download")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadSelections(int id)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery == null) return NotFound();

        var selections = await _db.Selections
            .Include(s => s.Photo)
            .Where(s => s.GalleryId == id)
            .ToListAsync();

        if (selections.Count == 0)
            return BadRequest(new { message = "No selections to download." });

        var files = selections.Select(s => (
            FilePath: _storage.GetAbsolutePath(s.Photo.OriginalPath),
            EntryName: s.Photo.FileName
        ));

        var zipStream = await _zip.CreateZipAsync(files);
        var zipName = $"{gallery.Name}_selections.zip"
            .Replace(" ", "_")
            .Replace("/", "-");

        return File(zipStream, "application/zip", zipName);
    }

    // ── Update photo sort order ───────────────────────────────────────────

    /// <summary>Updates the display order of photos in a gallery.</summary>
    /// <param name="id">Gallery database ID.</param>
    /// <param name="request">List of photo IDs with their new sort orders.</param>
    [HttpPut("{id:int}/photos/order")]
    [Authorize(Roles = "Admin,Photographer")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateOrder(int id, [FromBody] UpdatePhotoOrderRequest request)
    {
        if (!await _db.Galleries.AnyAsync(g => g.Id == id)) return NotFound();

        var photoIds = request.Items.Select(i => i.PhotoId).ToList();
        var photos = await _db.Photos
            .Where(p => p.GalleryId == id && photoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in request.Items)
        {
            if (photos.TryGetValue(item.PhotoId, out var photo))
                photo.SortOrder = item.SortOrder;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Update gallery status ─────────────────────────────────────────────

    /// <summary>Updates the status of a gallery to any valid value.</summary>
    /// <param name="id">Gallery database ID.</param>
    /// <param name="request">The new status value.</param>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin,Editor")]
    [ProducesResponseType(typeof(GalleryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        if (!Enum.IsDefined(typeof(GalleryStatus), request.Status))
            return BadRequest(new { message = "Invalid status value." });

        var gallery = await _db.Galleries
            .Include(g => g.Photos)
            .Include(g => g.Selections)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (gallery == null) return NotFound();

        gallery.Status = request.Status;
        await _db.SaveChangesAsync();
        return Ok(MapToDto(gallery));
    }

    // ── Regenerate previews for existing RAW photos ───────────────────────────

    /// <summary>
    /// Generates missing thumbnails and preview images for RAW photos in a gallery.
    /// Safe to call multiple times — skips photos that already have previews.
    /// </summary>
    [HttpPost("{id:int}/regenerate-previews")]
    [Authorize(Roles = "Admin,Photographer")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RegeneratePreviews(int id)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery == null) return NotFound();

        var photos = await _db.Photos
            .Where(p => p.GalleryId == id)
            .ToListAsync();

        int fixedCount = 0;
        foreach (var photo in photos)
        {
            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            bool changed = false;

            if (photo.ThumbnailPath == null && _thumbs.SupportsFormat(ext))
            {
                photo.ThumbnailPath = await _thumbs.GenerateThumbnailAsync(
                    photo.OriginalPath, gallery.UniqueToken, photo.FileName);
                changed = true;
            }

            if (photo.PreviewPath == null)
            {
                var preview = await _thumbs.GeneratePreviewAsync(
                    photo.OriginalPath, gallery.UniqueToken, photo.FileName);
                if (preview != null) { photo.PreviewPath = preview; changed = true; }
            }

            if (changed) fixedCount++;
        }

        await _db.SaveChangesAsync();
        return Ok(new { fixedCount, total = photos.Count });
    }

    // kept for backwards compatibility
    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkComplete(int id)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery == null) return NotFound();
        gallery.Status = GalleryStatus.Complete;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Rotate photo ─────────────────────────────────────────────────────

    [HttpPost("{id:int}/photos/{photoId:int}/rotate")]
    [Authorize(Roles = "Admin,Photographer")]
    [ProducesResponseType(typeof(PhotoDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RotatePhoto(int id, int photoId)
    {
        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.GalleryId == id);
        if (photo == null) return NotFound();

        photo.Rotation = (photo.Rotation + 90) % 360;
        await _db.SaveChangesAsync();
        return Ok(MapPhotoToDto(photo));
    }

    // ── Generate preview for a single photo ──────────────────────────────

    [HttpPost("{id:int}/photos/{photoId:int}/generate-preview")]
    [Authorize(Roles = "Admin,Photographer")]
    [ProducesResponseType(typeof(PhotoDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GeneratePhotoPreview(int id, int photoId)
    {
        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.GalleryId == id);
        if (photo == null) return NotFound();

        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery == null) return NotFound();

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (_thumbs.SupportsFormat(ext))
        {
            photo.ThumbnailPath = await _thumbs.GenerateThumbnailAsync(
                photo.OriginalPath, gallery.UniqueToken, photo.FileName);
            photo.PreviewPath = await _thumbs.GeneratePreviewAsync(
                photo.OriginalPath, gallery.UniqueToken, photo.FileName);
            await _db.SaveChangesAsync();
        }

        return Ok(MapPhotoToDto(photo));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private GalleryDto MapToDto(Gallery g) => new(
        g.Id, g.Name, g.ClientName, g.ClientEmail,
        g.MaxSelections, g.PasswordHash != null,
        g.UniqueToken, g.CreatedAt, g.Status,
        g.Photos.Count, g.Selections.Count
    );

    private PhotoDto MapPhotoToDto(Photo p) => new(
        p.Id, p.GalleryId, p.FileName,
        GetThumbnailUrl(p), GetOriginalUrl(p),
        p.PreviewPath != null ? _storage.GetPublicUrl(p.PreviewPath) : null,
        p.UploadedAt, p.SortOrder, p.Rotation
    );

    private string GetThumbnailUrl(Photo p)
        => p.ThumbnailPath != null
            ? _storage.GetPublicUrl(p.ThumbnailPath)
            : _storage.GetPublicUrl(p.OriginalPath);

    private string GetOriginalUrl(Photo p)
        => _storage.GetPublicUrl(p.OriginalPath);
}

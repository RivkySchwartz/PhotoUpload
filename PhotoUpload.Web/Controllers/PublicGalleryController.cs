using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoUpload.Data;
using PhotoUpload.Data.Models;
using PhotoUpload.Web.DTOs;
using PhotoUpload.Web.Services;

namespace PhotoUpload.Web.Controllers;

/// <summary>Public (unauthenticated) endpoints for client gallery access.</summary>
[ApiController]
[Route("api/gallery")]
public class PublicGalleryController : ControllerBase
{
    private readonly photoDataContext _db;
    private readonly IFileStorageService _storage;
    private readonly ICollageService _collage;
    private readonly IEmailService _email;

    public PublicGalleryController(
        photoDataContext db,
        IFileStorageService storage,
        ICollageService collage,
        IEmailService email)
    {
        _db = db;
        _storage = storage;
        _collage = collage;
        _email = email;
    }

    // ── Get gallery by token ───────────────────────────────────────────────

    /// <summary>Returns public gallery info by its unique shareable token.</summary>
    /// <param name="token">The gallery's unique token.</param>
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(GalleryPublicDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByToken(string token)
    {
        var gallery = await _db.Galleries.FirstOrDefaultAsync(g => g.UniqueToken == token);
        if (gallery == null) return NotFound();

        return Ok(new GalleryPublicDto(
            gallery.Id,
            gallery.Name,
            gallery.ClientName,
            gallery.MaxSelections,
            gallery.PasswordHash != null,
            gallery.Status
        ));
    }

    // ── Verify password ────────────────────────────────────────────────────

    /// <summary>Verifies the password for a password-protected gallery.</summary>
    /// <param name="token">The gallery's unique token.</param>
    /// <param name="request">The password to verify.</param>
    [HttpPost("{token}/verify")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> VerifyPassword(string token, [FromBody] VerifyPasswordRequest request)
    {
        var gallery = await _db.Galleries.FirstOrDefaultAsync(g => g.UniqueToken == token);
        if (gallery == null) return NotFound();

        if (gallery.PasswordHash == null) return Ok(new { verified = true });

        if (!BCrypt.Net.BCrypt.Verify(request.Password, gallery.PasswordHash))
            return Unauthorized(new { message = "Incorrect password." });

        return Ok(new { verified = true });
    }

    // ── Get photos ─────────────────────────────────────────────────────────

    /// <summary>Returns all photos in a gallery for client viewing.</summary>
    /// <param name="token">The gallery's unique token.</param>
    [HttpGet("{token}/photos")]
    [ProducesResponseType(typeof(IEnumerable<PhotoDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPhotos(string token)
    {
        var gallery = await _db.Galleries.FirstOrDefaultAsync(g => g.UniqueToken == token);
        if (gallery == null) return NotFound();

        var photos = await _db.Photos
            .Where(p => p.GalleryId == gallery.Id)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        return Ok(photos.Select(p => new PhotoDto(
            p.Id,
            p.GalleryId,
            p.FileName,
            GetThumbnailUrl(p),
            GetOriginalUrl(p),
            p.PreviewPath != null ? _storage.GetPublicUrl(p.PreviewPath) : null,
            p.UploadedAt,
            p.SortOrder,
            p.Rotation
        )));
    }

    // ── Submit selections ──────────────────────────────────────────────────

    /// <summary>Submits a client's final photo selections for a gallery.</summary>
    /// <param name="token">The gallery's unique token.</param>
    /// <param name="request">List of selected photo IDs.</param>
    [HttpPost("{token}/selections")]
    [ProducesResponseType(typeof(IEnumerable<SelectionDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SubmitSelections(string token, [FromBody] SubmitSelectionsRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var gallery = await _db.Galleries
            .Include(g => g.Photos)
            .FirstOrDefaultAsync(g => g.UniqueToken == token);

        if (gallery == null) return NotFound();

        // Allow more than MaxSelections only when the client explicitly upgraded.
        // The frontend tracks the upgrade state; the server accepts any count.

        var validPhotoIds = gallery.Photos.Select(p => p.Id).ToHashSet();
        var invalidIds = request.PhotoIds.Where(id => !validPhotoIds.Contains(id)).ToList();
        if (invalidIds.Any())
            return BadRequest(new { message = "One or more photo IDs are invalid for this gallery." });

        // Clear previous selections and print orders
        var existing = await _db.Selections.Where(s => s.GalleryId == gallery.Id).ToListAsync();
        _db.Selections.RemoveRange(existing);
        var existingPrints = await _db.PrintOrders.Where(p => p.GalleryId == gallery.Id).ToListAsync();
        _db.PrintOrders.RemoveRange(existingPrints);

        var now = DateTime.UtcNow;
        var newSelections = request.PhotoIds.Distinct().Select(photoId => new Selection
        {
            GalleryId = gallery.Id,
            PhotoId = photoId,
            SelectedAt = now
        }).ToList();

        _db.Selections.AddRange(newSelections);

        // Save print orders
        var newPrintOrders = (request.PrintOrders ?? [])
            .Where(p => validPhotoIds.Contains(p.PhotoId) && p.Quantity > 0)
            .Select(p => new PrintOrder
            {
                GalleryId = gallery.Id,
                PhotoId   = p.PhotoId,
                Size      = p.Size,
                Quantity  = p.Quantity,
                CreatedAt = now
            }).ToList();
        _db.PrintOrders.AddRange(newPrintOrders);

        gallery.Status = request.PhotoIds.Count > 0
            ? GalleryStatus.SelectionsMade
            : GalleryStatus.Pending;

        await _db.SaveChangesAsync();

        // Build selection DTOs
        var photosById = gallery.Photos.ToDictionary(p => p.Id);
        var selectionDtos = newSelections.Select(s => new SelectionDto(
            s.Id,
            s.PhotoId,
            photosById[s.PhotoId].FileName,
            GetThumbnailUrl(photosById[s.PhotoId]),
            s.SelectedAt
        )).ToList();

        // Generate collage from thumbnails of selected photos
        var imagePaths = newSelections
            .Select(s => photosById[s.PhotoId])
            .Select(p => _storage.GetAbsolutePath(p.PreviewPath ?? p.ThumbnailPath ?? p.OriginalPath))
            .ToList();

        var collageRelPath = await _collage.GenerateCollageAsync(imagePaths, gallery.UniqueToken);
        string? collageUrl = collageRelPath != null ? _storage.GetPublicUrl(collageRelPath) : null;

        // Send email if the gallery has a client email and collage was generated
        var emailSent = false;
        if (!string.IsNullOrWhiteSpace(gallery.ClientEmail) && collageRelPath != null)
        {
            var collageAbs = _storage.GetAbsolutePath(collageRelPath);
            emailSent = await _email.SendCollageEmailAsync(
                gallery.ClientEmail,
                gallery.ClientName,
                gallery.Name,
                collageAbs);
        }

        var printOrderDtos = newPrintOrders.Select(p => new PrintOrderDto(
            p.Id, p.PhotoId, photosById[p.PhotoId].FileName,
            GetThumbnailUrl(photosById[p.PhotoId]), p.Size, p.Quantity
        )).ToList();

        return Ok(new SubmitSelectionsResponse(selectionDtos, printOrderDtos, collageUrl, emailSent));
    }

    // ── Get existing selections ────────────────────────────────────────────

    /// <summary>Returns previously submitted selections for a gallery (for resuming).</summary>
    /// <param name="token">The gallery's unique token.</param>
    [HttpGet("{token}/selections")]
    [ProducesResponseType(typeof(IEnumerable<SelectionDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSelections(string token)
    {
        var gallery = await _db.Galleries.FirstOrDefaultAsync(g => g.UniqueToken == token);
        if (gallery == null) return NotFound();

        var selections = await _db.Selections
            .Include(s => s.Photo)
            .Where(s => s.GalleryId == gallery.Id)
            .ToListAsync();

        return Ok(selections.Select(s => new SelectionDto(
            s.Id, s.PhotoId, s.Photo.FileName,
            GetThumbnailUrl(s.Photo), s.SelectedAt
        )));
    }

    // ── Rotate photo ──────────────────────────────────────────────────────

    /// <summary>Rotates a photo 90° clockwise. Accessible with only the gallery token.</summary>
    [HttpPost("{token}/photos/{photoId:int}/rotate")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RotatePhoto(string token, int photoId)
    {
        var gallery = await _db.Galleries.FirstOrDefaultAsync(g => g.UniqueToken == token);
        if (gallery == null) return NotFound();

        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.GalleryId == gallery.Id);
        if (photo == null) return NotFound();

        photo.Rotation = (photo.Rotation + 90) % 360;
        await _db.SaveChangesAsync();
        return Ok(new { rotation = photo.Rotation });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private string GetThumbnailUrl(Photo p)
        => p.ThumbnailPath != null
            ? _storage.GetPublicUrl(p.ThumbnailPath)
            : _storage.GetPublicUrl(p.OriginalPath);

    private string GetOriginalUrl(Photo p)
        => _storage.GetPublicUrl(p.OriginalPath);
}

using System.ComponentModel.DataAnnotations;

namespace PhotoUpload.Data.Models;

public class Photo
{
    public int Id { get; set; }

    public int GalleryId { get; set; }

    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string OriginalPath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ThumbnailPath { get; set; }

    [MaxLength(1000)]
    public string? PreviewPath { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public int SortOrder { get; set; }

    public int Rotation { get; set; } // 0, 90, 180, 270

    public Gallery Gallery { get; set; } = null!;
    public ICollection<Selection> Selections { get; set; } = new List<Selection>();
}

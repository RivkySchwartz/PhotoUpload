using System.ComponentModel.DataAnnotations;

namespace PhotoUpload.Data.Models;

public enum GalleryStatus
{
    Pending = 0,
    SelectionsMade = 1,
    Editing = 2,
    Complete = 3
}

public class Gallery
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ClientName { get; set; } = string.Empty;

    [MaxLength(320)]
    public string? ClientEmail { get; set; }

    public int MaxSelections { get; set; }

    public string? PasswordHash { get; set; }

    [Required, MaxLength(64)]
    public string UniqueToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GalleryStatus Status { get; set; } = GalleryStatus.Pending;

    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
    public ICollection<Selection> Selections { get; set; } = new List<Selection>();
}

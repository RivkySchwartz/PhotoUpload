using System.ComponentModel.DataAnnotations;

namespace PhotoUpload.Data.Models;

public class PrintOrder
{
    public int Id { get; set; }

    public int GalleryId { get; set; }

    public int PhotoId { get; set; }

    [Required, MaxLength(20)]
    public string Size { get; set; } = string.Empty; // "4x6" | "5x7" | "8x10" | "11x14" | "16x20"

    public int Quantity { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Gallery Gallery { get; set; } = null!;
    public Photo    Photo   { get; set; } = null!;
}

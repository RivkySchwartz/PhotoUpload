namespace PhotoUpload.Data.Models;

public class Selection
{
    public int Id { get; set; }

    public int GalleryId { get; set; }

    public int PhotoId { get; set; }

    public DateTime SelectedAt { get; set; } = DateTime.UtcNow;

    public Gallery Gallery { get; set; } = null!;
    public Photo Photo { get; set; } = null!;
}

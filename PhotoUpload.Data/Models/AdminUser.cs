using System.ComponentModel.DataAnnotations;

namespace PhotoUpload.Data.Models;

public enum UserRole
{
    Admin = 0,
    Photographer = 1,
    Editor = 2,
    Secretary = 3
}

public class AdminUser
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Admin;

    public bool MustChangePassword { get; set; } = false;
}

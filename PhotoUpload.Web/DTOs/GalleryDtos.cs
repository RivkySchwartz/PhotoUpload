using PhotoUpload.Data.Models;
using System.ComponentModel.DataAnnotations;

namespace PhotoUpload.Web.DTOs;

public record CreateGalleryRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(200)] string ClientName,
    [MaxLength(320)] string? ClientEmail,
    [Range(1, 10000)] int MaxSelections,
    string? Password
);

public record GalleryDto(
    int Id,
    string Name,
    string ClientName,
    string? ClientEmail,
    int MaxSelections,
    bool IsPasswordProtected,
    string UniqueToken,
    DateTime CreatedAt,
    GalleryStatus Status,
    int PhotoCount,
    int SelectionCount
);

public record GalleryPublicDto(
    int Id,
    string Name,
    string ClientName,
    int MaxSelections,
    bool IsPasswordProtected,
    GalleryStatus Status
);

public record VerifyPasswordRequest([Required] string Password);

public record PhotoDto(
    int Id,
    int GalleryId,
    string FileName,
    string ThumbnailUrl,
    string OriginalUrl,
    string? PreviewUrl,
    DateTime UploadedAt,
    int SortOrder,
    int Rotation
);

public record SubmitSelectionsRequest(
    [Required] List<int> PhotoIds,
    List<PrintOrderItemRequest>? PrintOrders
);

public record PrintOrderItemRequest(int PhotoId, [Required, MaxLength(20)] string Size, int Quantity = 1);

public record PrintOrderDto(int Id, int PhotoId, string FileName, string ThumbnailUrl, string Size, int Quantity);

public record SelectionDto(
    int Id,
    int PhotoId,
    string FileName,
    string ThumbnailUrl,
    DateTime SelectedAt
);

public record UpdatePhotoOrderRequest(
    [Required] List<PhotoOrderItem> Items
);

public record PhotoOrderItem(int PhotoId, int SortOrder);

public record UpdateStatusRequest([Required] GalleryStatus Status);

public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);

public record LoginResponse(string Token, string Username, string Role, bool MustChangePassword);

public record UserDto(int Id, string Username, string Role, bool MustChangePassword);

public record CreateUserRequest(
    [Required, MaxLength(100)] string Username,
    [Required] string Role,
    [Required, MinLength(6)] string TemporaryPassword
);

public record ChangePasswordRequest([Required, MinLength(6)] string NewPassword);

public record SubmitSelectionsResponse(
    IEnumerable<SelectionDto> Selections,
    IEnumerable<PrintOrderDto> PrintOrders,
    string? CollageUrl,
    bool EmailSent
);

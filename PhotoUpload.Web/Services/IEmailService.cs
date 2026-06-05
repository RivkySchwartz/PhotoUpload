namespace PhotoUpload.Web.Services;

public interface IEmailService
{
    Task<bool> SendCollageEmailAsync(
        string toEmail,
        string clientName,
        string galleryName,
        string collageAbsolutePath);
}

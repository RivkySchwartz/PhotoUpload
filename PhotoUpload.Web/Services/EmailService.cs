using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PhotoUpload.Web.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendCollageEmailAsync(
        string toEmail,
        string clientName,
        string galleryName,
        string collageAbsolutePath)
    {
        var host     = _config["Smtp:Host"];
        var fromEmail = _config["Smtp:FromEmail"];
        var username = _config["Smtp:Username"];
        var password = _config["Smtp:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail) ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("SMTP is not configured — skipping email");
            return false;
        }

        try
        {
            var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
            var fromName = _config["Smtp:FromName"] ?? "PhotoSelect";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(clientName, toEmail));
            message.Subject = $"Your photo selections — {galleryName}";

            var builder = new BodyBuilder();

            if (File.Exists(collageAbsolutePath))
            {
                var linkedResource = builder.LinkedResources.Add(collageAbsolutePath);
                linkedResource.ContentId = "collage";
                linkedResource.ContentType.Name = "collage.jpg";

                builder.HtmlBody = $"""
                    <div style="font-family:sans-serif;max-width:600px;margin:0 auto;background:#111;color:#eee;padding:32px;border-radius:12px;">
                      <h2 style="color:#fff;margin-top:0">Hi {clientName}! 🎉</h2>
                      <p style="color:#ccc;font-size:16px;">
                        Thank you for selecting your photos from <strong style="color:#fff">{galleryName}</strong>.<br/>
                        Here is a collage of your selected photos:
                      </p>
                      <img src="cid:collage" alt="Your photo collage"
                           style="width:100%;border-radius:8px;margin:16px 0;" />
                      <p style="color:#999;font-size:13px;margin-top:24px;">
                        Your photographer will be in touch soon!
                      </p>
                    </div>
                    """;

                builder.Attachments.Add(collageAbsolutePath);
            }
            else
            {
                builder.HtmlBody = $"""
                    <div style="font-family:sans-serif;max-width:600px;margin:0 auto;background:#111;color:#eee;padding:32px;border-radius:12px;">
                      <h2 style="color:#fff;margin-top:0">Hi {clientName}! 🎉</h2>
                      <p style="color:#ccc;font-size:16px;">
                        Thank you for selecting your photos from <strong style="color:#fff">{galleryName}</strong>.
                        Your photographer will be in touch soon!
                      </p>
                    </div>
                    """;
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Collage email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send collage email to {Email}", toEmail);
            return false;
        }
    }
}

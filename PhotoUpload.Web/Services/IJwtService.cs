namespace PhotoUpload.Web.Services;

public interface IJwtService
{
    string GenerateToken(int userId, string username, string role);
}

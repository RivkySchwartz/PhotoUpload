using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PhotoUpload.Data;
using PhotoUpload.Data.Models;
using PhotoUpload.Web.Services;
using System.Text;

namespace PhotoUpload.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ── Database ──────────────────────────────────────────────────────
        var connStr = builder.Configuration.GetConnectionString("ConStr")!;
        builder.Services.AddDbContext<photoDataContext>(opts =>
            opts.UseSqlServer(connStr));

        // ── Services ──────────────────────────────────────────────────────
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
        builder.Services.AddScoped<IThumbnailService, ThumbnailService>();
        builder.Services.AddScoped<IZipService, ZipService>();
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<ICollageService, CollageService>();
        builder.Services.AddScoped<IEmailService, EmailService>();

        // ── JWT Authentication ────────────────────────────────────────────
        var jwtSecret = builder.Configuration["Jwt:Secret"]!;
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddControllers()
            .AddJsonOptions(opts =>
                opts.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter()));
        builder.Services.AddEndpointsApiExplorer();

        // ── CORS ──────────────────────────────────────────────────────────
        builder.Services.AddCors(opts =>
        {
            opts.AddDefaultPolicy(policy =>
            {
                if (builder.Environment.IsDevelopment())
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                else
                    policy.WithOrigins(
                              builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
                              ?? [])
                          .AllowAnyMethod()
                          .AllowAnyHeader();
            });
        });

        builder.WebHost.ConfigureKestrel(o =>
            o.Limits.MaxRequestBodySize = 2_000_000_000L);

        var app = builder.Build();

        // ── Auto-migrate & seed ───────────────────────────────────────────
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<photoDataContext>();
            await db.Database.MigrateAsync();
            await SeedAdminUser(db, builder.Configuration);
        }

        // ── Static files: serve uploads directory ─────────────────────────
        var storageRoot = builder.Configuration["Storage:RootPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        Directory.CreateDirectory(storageRoot);

        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".cr2"] = "image/x-canon-cr2";
        provider.Mappings[".nef"] = "image/x-nikon-nef";
        provider.Mappings[".arw"] = "image/x-sony-arw";
        provider.Mappings[".heic"] = "image/heic";

        if (!app.Environment.IsDevelopment())
            app.UseHsts();

        app.UseCors();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                Path.GetFullPath(storageRoot)),
            RequestPath = "/uploads",
            ContentTypeProvider = provider
        });
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseStaticFiles();

        app.MapControllers();
        app.MapFallbackToFile("index.html");

        await app.RunAsync();
    }

    private static async Task SeedAdminUser(photoDataContext db, IConfiguration config)
    {
        var username = config["Admin:DefaultUsername"] ?? "admin";
        var password = config["Admin:DefaultPassword"] ?? "Admin@123!";

        var existing = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (existing != null)
        {
            var changed = false;
            if (!BCrypt.Net.BCrypt.Verify(password, existing.PasswordHash))
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                changed = true;
            }
            if (existing.Role != UserRole.Admin) { existing.Role = UserRole.Admin; changed = true; }
            if (existing.MustChangePassword) { existing.MustChangePassword = false; changed = true; }
            if (changed) await db.SaveChangesAsync();
        }
        else
        {
            db.AdminUsers.Add(new AdminUser
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Admin,
                MustChangePassword = false
            });
            await db.SaveChangesAsync();
        }
    }
}

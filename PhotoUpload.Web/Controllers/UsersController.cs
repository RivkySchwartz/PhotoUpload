using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoUpload.Data;
using PhotoUpload.Data.Models;
using PhotoUpload.Web.DTOs;
using System.Security.Claims;

namespace PhotoUpload.Web.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly photoDataContext _db;

    public UsersController(photoDataContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.AdminUsers
            .OrderBy(u => u.Username)
            .Select(u => new UserDto(u.Id, u.Username, u.Role.ToString(), u.MustChangePassword))
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            return BadRequest(new { message = "Invalid role." });

        if (await _db.AdminUsers.AnyAsync(u => u.Username == request.Username))
            return Conflict(new { message = "Username already exists." });

        var user = new AdminUser
        {
            Username = request.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword),
            Role = role,
            MustChangePassword = true
        };

        _db.AdminUsers.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new UserDto(user.Id, user.Username, user.Role.ToString(), user.MustChangePassword));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var selfId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (id == selfId)
            return BadRequest(new { message = "You cannot delete your own account." });

        var user = await _db.AdminUsers.FindAsync(id);
        if (user == null) return NotFound();

        _db.AdminUsers.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

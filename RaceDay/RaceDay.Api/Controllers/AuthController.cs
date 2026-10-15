using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public AuthController(RaceDayDbContext db)
    {
        _db = db;
    }

    [HttpPost("register")]
    [SwaggerOperation(
        Summary = "Register a new account",
        Description = "Creates an Organiser or Participant account. The password is hashed before it is stored.")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        // Only the two roles from the ERD are allowed
        if (request.Role != "Organiser" && request.Role != "Participant")
        {
            return BadRequest(new { message = "Role must be 'Organiser' or 'Participant'." });
        }

        string email = request.Email.Trim();

        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var user = new AppUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = request.Role,
            Phone = request.Phone
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = new AuthResponse
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            Message = "Registration successful."
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }
}
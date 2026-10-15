using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[RequireRole] // any logged-in user, Organiser or Participant
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db)
    {
        _db = db;
    }

    [HttpGet("me")]
    [SwaggerOperation(
        Summary = "Get my profile",
        Description = "Returns the profile of the logged-in user (taken from the session).")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.FindAsync(HttpContext.GetUserId());

        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(ToDto(user));
    }

    [HttpPut("me")]
    [SwaggerOperation(
        Summary = "Update my profile",
        Description = "Updates first name, last name and phone of the logged-in user. A user can only change their own profile.")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(HttpContext.GetUserId());

        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Phone = request.Phone;

        await _db.SaveChangesAsync();

        return Ok(ToDto(user));
    }

    private static ProfileDto ToDto(AppUser user) => new()
    {
        UserId = user.UserId,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Role = user.Role,
        Phone = user.Phone
    };
}
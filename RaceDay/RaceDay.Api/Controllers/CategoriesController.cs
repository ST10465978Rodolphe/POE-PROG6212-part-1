using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
[RequireRole] // any logged-in user can view; stricter rules are set per action
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    [HttpGet("events/{eventId:int}/categories")]
    [SwaggerOperation(
        Summary = "List the categories of an event",
        Description = "Returns every category (age group or distance) defined for the event. Available to Organisers and Participants.")]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFound(new { message = "Event not found." });
        }

        var categories = await _db.Categories
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.Distance)
            .ThenBy(c => c.CategoryName)
            .ToListAsync();

        return Ok(categories.Select(ToDto));
    }

    [HttpPost("events/{eventId:int}/categories")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "Create a category for an event",
        Description = "Organiser only, and only for events they own. The category name must be unique within the event (409 otherwise).")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(int eventId, CreateCategoryRequest request)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (ev.OrganiserId != HttpContext.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only add categories to your own events." });
        }

        string name = request.CategoryName.Trim();

        if (await _db.Categories.AnyAsync(c => c.EventId == eventId && c.CategoryName == name))
        {
            return Conflict(new { message = "This event already has a category with that name." });
        }

        var category = new Category
        {
            EventId = eventId,
            CategoryName = name,
            Description = request.Description?.Trim(),
            Distance = request.Distance,
            EntryFee = request.EntryFee
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, ToDto(category));
    }

    [HttpPut("categories/{id:int}")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "Update a category",
        Description = "Organiser only, and only for categories of events they own. The category name must stay unique within the event (409 otherwise).")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, CreateCategoryRequest request)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category == null)
        {
            return NotFound(new { message = "Category not found." });
        }

        if (category.Event!.OrganiserId != HttpContext.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only change categories of your own events." });
        }

        string name = request.CategoryName.Trim();

        bool nameTaken = await _db.Categories.AnyAsync(c =>
            c.EventId == category.EventId &&
            c.CategoryName == name &&
            c.CategoryId != id);

        if (nameTaken)
        {
            return Conflict(new { message = "This event already has a category with that name." });
        }

        category.CategoryName = name;
        category.Description = request.Description?.Trim();
        category.Distance = request.Distance;
        category.EntryFee = request.EntryFee;

        await _db.SaveChangesAsync();

        return Ok(ToDto(category));
    }

    [HttpDelete("categories/{id:int}")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "Delete a category",
        Description = "Organiser only, and only for categories of events they own. A category that already has enrolments cannot be deleted (409).")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category == null)
        {
            return NotFound(new { message = "Category not found." });
        }

        if (category.Event!.OrganiserId != HttpContext.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete categories of your own events." });
        }

        // Enrolments use Restrict in the database: answer clearly instead of a 500 error
        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
        {
            return Conflict(new { message = "This category already has enrolments and cannot be deleted." });
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Category deleted." });
    }

    private static CategoryDto ToDto(Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        Description = c.Description,
        Distance = c.Distance,
        EntryFee = c.EntryFee
    };
}
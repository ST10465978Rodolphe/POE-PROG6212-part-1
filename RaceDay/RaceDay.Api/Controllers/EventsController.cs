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
[Route("api/events")]
[RequireRole] // any logged-in user can view; stricter rules are set per action
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "List all events",
        Description = "Returns every event ordered by date. Available to Organisers and Participants.")]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var events = await _db.Events
            .Include(e => e.Organiser)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return Ok(events.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(
        Summary = "Get one event",
        Description = "Returns a single event by its id. Available to Organisers and Participants.")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        return Ok(ToDto(ev));
    }

    [HttpGet("mine")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "List my events",
        Description = "Organiser only. Returns only the events created by the logged-in Organiser.")]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        int userId = HttpContext.GetUserId();

        var events = await _db.Events
            .Include(e => e.Organiser)
            .Where(e => e.OrganiserId == userId)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return Ok(events.Select(ToDto));
    }

    private static EventDto ToDto(Event e) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser == null
            ? string.Empty
            : $"{e.Organiser.FirstName} {e.Organiser.LastName}",
        EventName = e.EventName,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        Distance = e.Distance,
        EventType = e.EventType
    };
}
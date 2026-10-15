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
    // Must match the CK_Event_Type check constraint in RaceDayDbContext
    private static readonly string[] ValidTypes = { "Running", "Walking", "Cycling" };

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

    [HttpPost]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "Create an event",
        Description = "Organiser only. Needs event name, date, location, distance and type (Running, Walking or Cycling). The event is linked to the logged-in Organiser.")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CreateEventRequest request)
    {
        string? type = NormaliseType(request.EventType);
        if (type == null)
        {
            return BadRequest(new { message = "EventType must be 'Running', 'Walking' or 'Cycling'." });
        }

        int userId = HttpContext.GetUserId();

        var ev = new Event
        {
            OrganiserId = userId,
            Organiser = await _db.Users.FindAsync(userId),
            EventName = request.EventName.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            Distance = request.Distance,
            EventType = type
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ToDto(ev));
    }

    [HttpPut("{id:int}")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "Update an event",
        Description = "Organiser only, and only for events they own.")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateEventRequest request)
    {
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (ev.OrganiserId != HttpContext.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only change your own events." });
        }

        string? type = NormaliseType(request.EventType);
        if (type == null)
        {
            return BadRequest(new { message = "EventType must be 'Running', 'Walking' or 'Cycling'." });
        }

        ev.EventName = request.EventName.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.Distance = request.Distance;
        ev.EventType = type;

        await _db.SaveChangesAsync();

        return Ok(ToDto(ev));
    }

    [HttpDelete("{id:int}")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "Delete an event",
        Description = "Organiser only, and only for events they own. Its categories and route are removed too. An event that already has enrolments cannot be deleted (409).")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories).ThenInclude(c => c.Enrolments)
            .Include(e => e.Route)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (ev.OrganiserId != HttpContext.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete your own events." });
        }

        // The database uses Restrict, so enrolments would block the delete anyway.
        // Give a clear answer instead of a 500 error.
        if (ev.Categories.Any(c => c.Enrolments.Any()))
        {
            return Conflict(new { message = "This event already has enrolments and cannot be deleted." });
        }

        if (ev.Route != null)
        {
            _db.Routes.Remove(ev.Route);
        }
        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);

        await _db.SaveChangesAsync();

        return Ok(new { message = "Event deleted." });
    }

    // Returns the correctly-cased type, or null if it is not one of the three allowed types
    private static string? NormaliseType(string value) =>
        ValidTypes.FirstOrDefault(t => t.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

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
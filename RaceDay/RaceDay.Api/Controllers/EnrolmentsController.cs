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
[Route("api/enrolments")]
[RequireRole] // must be logged in; the role is set per action
public class EnrolmentsController : ControllerBase
{
    // Must match the CK_Enrolment_Status check constraint in RaceDayDbContext
    private static readonly string[] ValidStatuses = { "Confirmed", "Cancelled" };

    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [RequireRole("Participant")]
    [SwaggerOperation(
        Summary = "Enter an event",
        Description = "Participant only. Enters the logged-in Participant into an event by choosing one of that event's categories. The category must belong to the event (400), the event must not have passed (400) and a Participant can only hold one active enrolment per event (409).")]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateEnrolmentRequest request)
    {
        var ev = await _db.Events.FindAsync(request.EventId);
        if (ev == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        // The chosen category must belong to the chosen event
        var category = await _db.Categories.FirstOrDefaultAsync(c =>
            c.CategoryId == request.CategoryId && c.EventId == request.EventId);
        if (category == null)
        {
            return BadRequest(new { message = "That category does not belong to this event." });
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (ev.EventDate < today)
        {
            return BadRequest(new { message = "This event has already taken place." });
        }

        int userId = HttpContext.GetUserId();

        // Everything this Participant already has in this event
        var existing = await _db.Enrolments
            .Include(e => e.Category)
            .Where(e => e.ParticipantId == userId && e.Category!.EventId == request.EventId)
            .ToListAsync();

        if (existing.Any(e => e.Status == "Confirmed"))
        {
            return Conflict(new { message = "You are already enrolled in this event." });
        }

        // The database allows one row per Participant and Category, so a cancelled
        // enrolment for the same category is switched back on instead of duplicated.
        var cancelled = existing.FirstOrDefault(e => e.CategoryId == request.CategoryId);

        Enrolment enrolment;
        if (cancelled != null)
        {
            cancelled.Status = "Confirmed";
            cancelled.EnrolmentDate = today;
            enrolment = cancelled;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantId = userId,
                CategoryId = request.CategoryId,
                EnrolmentDate = today,
                Status = "Confirmed"
            };
            _db.Enrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync();

        var saved = await WithDetails().FirstAsync(e => e.EnrolmentId == enrolment.EnrolmentId);
        return StatusCode(StatusCodes.Status201Created, ToDto(saved));
    }

    [HttpGet("mine")]
    [RequireRole("Participant")]
    [SwaggerOperation(
        Summary = "List my enrolments",
        Description = "Participant only. Returns the enrolments of the logged-in Participant, newest first.")]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        int userId = HttpContext.GetUserId();

        var enrolments = await WithDetails()
            .Where(e => e.ParticipantId == userId)
            .OrderByDescending(e => e.EnrolmentDate)
            .ThenByDescending(e => e.EnrolmentId)
            .ToListAsync();

        return Ok(enrolments.Select(ToDto));
    }

    [HttpGet("event/{eventId:int}")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "List the enrolments of an event",
        Description = "Organiser only, and only for events they own. Returns every enrolment for the event with the Participant and category.")]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (ev.OrganiserId != HttpContext.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view enrolments for your own events." });
        }

        var enrolments = await WithDetails()
            .Where(e => e.Category!.EventId == eventId)
            .OrderBy(e => e.Participant!.LastName)
            .ThenBy(e => e.Participant!.FirstName)
            .ToListAsync();

        return Ok(enrolments.Select(ToDto));
    }

    [HttpPut("{id:int}/status")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "Change the status of an enrolment",
        Description = "Organiser only, and only for enrolments in events they own. Status must be 'Confirmed' or 'Cancelled'.")]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateEnrolmentStatusRequest request)
    {
        var enrolment = await WithDetails().FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment == null)
        {
            return NotFound(new { message = "Enrolment not found." });
        }

        if (enrolment.Category!.Event!.OrganiserId != HttpContext.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only manage enrolments for your own events." });
        }

        string? status = ValidStatuses.FirstOrDefault(s =>
            s.Equals(request.Status.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status == null)
        {
            return BadRequest(new { message = "Status must be 'Confirmed' or 'Cancelled'." });
        }

        enrolment.Status = status;
        await _db.SaveChangesAsync();

        return Ok(ToDto(enrolment));
    }

    // Loads the related Participant, Category and Event so the DTO can be filled in
    private IQueryable<Enrolment> WithDetails() => _db.Enrolments
        .Include(e => e.Participant)
        .Include(e => e.Category)
            .ThenInclude(c => c!.Event);

    private static EnrolmentDto ToDto(Enrolment e) => new()
    {
        EnrolmentId = e.EnrolmentId,
        ParticipantId = e.ParticipantId,
        ParticipantName = e.Participant == null
            ? string.Empty
            : $"{e.Participant.FirstName} {e.Participant.LastName}",
        CategoryId = e.CategoryId,
        CategoryName = e.Category?.CategoryName ?? string.Empty,
        EventId = e.Category?.EventId ?? 0,
        EventName = e.Category?.Event?.EventName ?? string.Empty,
        EnrolmentDate = e.EnrolmentDate,
        Status = e.Status
    };
}
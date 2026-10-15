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
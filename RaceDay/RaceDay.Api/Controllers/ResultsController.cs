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
[Route("api/results")]
[RequireRole] // must be logged in; the role is set per action
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "Capture a result",
        Description = "Organiser only, and only for enrolments in events they own. Records the finish time (hh:mm:ss) and finishing position for a confirmed enrolment. An enrolment can only have one result (409).")]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category)
                .ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == request.EnrolmentId);

        if (enrolment == null)
        {
            return NotFound(new { message = "Enrolment not found." });
        }

        // The Organiser must own the event this enrolment belongs to
        if (enrolment.Category!.Event!.OrganiserId != HttpContext.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only capture results for your own events." });
        }

        if (enrolment.Status != "Confirmed")
        {
            return BadRequest(new { message = "Results can only be captured for confirmed enrolments." });
        }

        // The database column is a TIME value, so it must be a time within one day
        if (request.FinishTime <= TimeSpan.Zero || request.FinishTime >= TimeSpan.FromDays(1))
        {
            return BadRequest(new { message = "FinishTime must be greater than 00:00:00 and less than 24 hours." });
        }

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == request.EnrolmentId))
        {
            return Conflict(new { message = "A result has already been captured for this enrolment." });
        }

        var result = new Result
        {
            EnrolmentId = request.EnrolmentId,
            FinishTime = request.FinishTime,
            FinishingPosition = request.Position,
            ResultDate = DateOnly.FromDateTime(DateTime.Today)
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var saved = await WithDetails().FirstAsync(r => r.ResultId == result.ResultId);
        return StatusCode(StatusCodes.Status201Created, ToDto(saved));
    }

    [HttpGet("event/{eventId:int}")]
    [RequireRole("Organiser")]
    [SwaggerOperation(
        Summary = "List the results of an event",
        Description = "Organiser only, and only for events they own. Returns all captured results for the event, ordered by finishing position.")]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
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
                new { message = "You can only view results for your own events." });
        }

        var results = await WithDetails()
            .Where(r => r.Enrolment!.Category!.EventId == eventId)
            .OrderBy(r => r.Enrolment!.Category!.CategoryName)
            .ThenBy(r => r.FinishingPosition)
            .ToListAsync();

        return Ok(results.Select(ToDto));
    }

    // Loads the Enrolment, Participant, Category and Event so the DTO can be filled in
    private IQueryable<Result> WithDetails() => _db.Results
        .Include(r => r.Enrolment)
            .ThenInclude(e => e!.Participant)
        .Include(r => r.Enrolment)
            .ThenInclude(e => e!.Category)
                .ThenInclude(c => c!.Event);

    private static ResultDto ToDto(Result r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantName = r.Enrolment?.Participant == null
            ? string.Empty
            : $"{r.Enrolment.Participant.FirstName} {r.Enrolment.Participant.LastName}",
        EventName = r.Enrolment?.Category?.Event?.EventName ?? string.Empty,
        CategoryName = r.Enrolment?.Category?.CategoryName ?? string.Empty,
        FinishTime = r.FinishTime,
        Position = r.FinishingPosition,
        ResultDate = r.ResultDate
    };
}
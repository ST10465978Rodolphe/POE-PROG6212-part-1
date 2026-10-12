namespace RaceDay.Api.Models;

public class Enrolment
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public AppUser? Participant { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public DateOnly EnrolmentDate { get; set; }
    public string Status { get; set; } = "Confirmed";

    public Result? Result { get; set; }
}
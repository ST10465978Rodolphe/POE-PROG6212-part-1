namespace RaceDay.Api.Models;

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public AppUser? Organiser { get; set; }

    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public string EventType { get; set; } = string.Empty;

    public List<Category> Categories { get; set; } = new();
    public EventRoute? Route { get; set; }
}
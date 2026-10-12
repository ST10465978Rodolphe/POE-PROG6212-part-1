namespace RaceDay.Api.Models;

// Named EventRoute (not Route) to avoid a clash with ASP.NET's own Route class.
// It is still stored in the "Route" table from the Part 1 ERD.
public class EventRoute
{
    public int RouteId { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public string RouteName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string FinishLocation { get; set; } = string.Empty;
    public decimal Distance { get; set; }
}
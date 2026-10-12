namespace RaceDay.Api.Models;

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }

    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Distance { get; set; }
    public decimal EntryFee { get; set; }

    public List<Enrolment> Enrolments { get; set; } = new();
}
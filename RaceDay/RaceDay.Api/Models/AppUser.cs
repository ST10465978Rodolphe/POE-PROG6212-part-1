namespace RaceDay.Api.Models;

public class AppUser
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Only the hash is stored, never the original password
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public List<Event> Events { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}
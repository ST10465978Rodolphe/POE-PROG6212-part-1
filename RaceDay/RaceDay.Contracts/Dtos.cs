using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts;

// ---------- Authentication ----------
public class RegisterRequest
{
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), StringLength(100)]
    public string Password { get; set; } = string.Empty;

    // "Organiser" or "Participant"
    [Required]
    public string Role { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// ---------- Profile ----------
public class ProfileDto
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class UpdateProfileRequest
{
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }
}

// ---------- Events ----------
public class CreateEventRequest
{
    [Required, StringLength(100)]
    public string EventName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    public DateOnly EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Range(0.01, 1000)]
    public decimal Distance { get; set; }

    // "Running", "Walking" or "Cycling"
    [Required, StringLength(30)]
    public string EventType { get; set; } = string.Empty;
}

public class UpdateEventRequest : CreateEventRequest
{
}

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public string EventType { get; set; } = string.Empty;
}

// ---------- Categories ----------
public class CreateCategoryRequest
{
    [Required, StringLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    [Range(0.01, 1000)]
    public decimal Distance { get; set; }

    [Range(0, 100000)]
    public decimal EntryFee { get; set; }
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Distance { get; set; }
    public decimal EntryFee { get; set; }
}

// ---------- Enrolments ----------
public class CreateEnrolmentRequest
{
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

public class UpdateEnrolmentStatusRequest
{
    // "Confirmed" or "Cancelled"
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ---------- Results ----------
public class CreateResultRequest
{
    public TimeSpan FinishTime { get; set; }

    [Range(1, 1000000)]
    public int Position { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }
    public DateOnly ResultDate { get; set; }
}

// ---------- Routes ----------
public class RouteRequest
{
    [Required, StringLength(100)]
    public string RouteName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    [Required, StringLength(150)]
    public string StartLocation { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string FinishLocation { get; set; } = string.Empty;

    [Range(0.01, 1000)]
    public decimal Distance { get; set; }
}

public class RouteDto
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string FinishLocation { get; set; } = string.Empty;
    public decimal Distance { get; set; }
}
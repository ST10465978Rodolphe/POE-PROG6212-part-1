namespace RaceDay.Api.Services;

// Small helpers to read the logged-in user from the session.
// Only call these after [RequireRole] has checked that the user is logged in.
public static class UserSessionExtensions
{
    public static int GetUserId(this HttpContext context) =>
        context.Session.GetInt32(SessionKeys.UserId) ?? 0;

    public static string GetUserRole(this HttpContext context) =>
        context.Session.GetString(SessionKeys.Role) ?? string.Empty;
}
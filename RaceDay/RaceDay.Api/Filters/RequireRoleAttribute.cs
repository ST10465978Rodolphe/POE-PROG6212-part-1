using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Services;

namespace RaceDay.Api.Filters;

// Put this on a controller or an action to protect it.
//   [RequireRole]               -> any logged-in user (401 if not logged in)
//   [RequireRole("Organiser")]  -> only that role (401 if not logged in, 403 if wrong role)
// It is an authorization filter, so it runs BEFORE model validation.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public RequireRoleAttribute(params string[] roles)
    {
        _roles = roles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;

        int? userId = session.GetInt32(SessionKeys.UserId);
        string? role = session.GetString(SessionKeys.Role);

        // Not logged in
        if (userId == null || role == null)
        {
            context.Result = new UnauthorizedObjectResult(
                new { message = "You must be logged in to use this endpoint." });
            return;
        }

        // Logged in, but with the wrong role
        if (_roles.Length > 0 && !_roles.Contains(role))
        {
            context.Result = new ObjectResult(
                new { message = $"This endpoint is only available to: {string.Join(", ", _roles)}." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
using Classroom.Application.Abstractions;
using Classroom.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUser(IHttpContextAccessor http, UserManager<ApplicationUser> userManager)
    {
        _http = http;
        _userManager = userManager;
    }

    public string? UserId =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<ApplicationUser?> GetUserAsync()
    {
        var principal = _http.HttpContext?.User;
        if (principal == null) return null;

        return await _userManager.GetUserAsync(principal);
    }

    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        return _http.HttpContext?.User?.IsInRole(role) == true;
    }

}

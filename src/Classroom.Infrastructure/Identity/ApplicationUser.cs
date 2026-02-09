using Microsoft.AspNetCore.Identity;

namespace Classroom.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? AdminId { get; set; } // new
}

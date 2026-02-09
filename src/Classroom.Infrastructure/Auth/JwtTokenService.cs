using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Classroom.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
    
namespace Classroom.Infrastructure.Auth;

public interface IJwtTokenService
{
    Task<(string token, DateTimeOffset expiresAt)> CreateAsync(ApplicationUser user);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opt;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(IOptions<JwtOptions> options, UserManager<ApplicationUser> userManager)
    {
        _opt = options.Value;
        _userManager = userManager;
    }

    public async Task<(string token, DateTimeOffset expiresAt)> CreateAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id), // <- add this for ASP.NET compatibility
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("name", user.FullName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = DateTimeOffset.UtcNow.AddMinutes(_opt.ExpMinutes);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
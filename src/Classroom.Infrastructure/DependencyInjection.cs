using Classroom.Application.Abstractions;
using Classroom.Infrastructure.Auth;
using Classroom.Infrastructure.Email;
using Classroom.Infrastructure.Identity;
using Classroom.Infrastructure.Persistence;
using Classroom.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Classroom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<LocalFileStorageOptions>(config.GetSection("Storage"));

        // Email config (optional)
        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.AddTransient<IEmailService, EmailService>();

        var conn = config.GetConnectionString("DefaultConnection")
                   ?? config["DATABASE_URL"]; 

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Missing connection string. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");

        // Enable dynamic JSON for Npgsql 8 so EF can write List<string> to jsonb columns
        NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();

        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()   // <-- ADDED: registers DataProtection token provider used by GeneratePasswordResetTokenAsync
            .AddSignInManager<SignInManager<ApplicationUser>>();

     
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }
}

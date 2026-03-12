using Classroom.Application.Abstractions;
using Classroom.Infrastructure.Auth;
using Classroom.Infrastructure.Email;
using Classroom.Infrastructure.FileStorage;
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

        services.AddMemoryCache();

        services.Configure<EmailOptions>(config.GetSection("Email"));
        services.AddScoped<IEmailService, EmailService>();

        var conn = config.GetConnectionString("DefaultConnection")
                   ?? config["DATABASE_URL"];

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Missing connection string. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");

        NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();

        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));

        // DB-backed verification store must be scoped because it uses AppDbContext (scoped).
        services.AddScoped<ITeacherEmailVerificationStore, TeacherEmailVerificationStore>();

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IFileStorage, GoogleCloudStorageFileStorage>();

        services.Configure<AdmissionsOptions>(config.GetSection("Admissions"));
        services.AddScoped<IAdmissionsValidator, DbAdmissionsValidator>();

        services.AddScoped<AdmissionsSeeder>();

        return services;
    }
}

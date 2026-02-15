using Classroom.Domain.Enums;
using Classroom.Infrastructure;
using Classroom.Infrastructure.Identity;
using Classroom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // optionally: o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

//
// Swagger + JWT auth button
//
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Classroom.Api", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Classroom.Application.Abstractions.ICurrentUser, CurrentUser>();

//
// Infrastructure (DbContext + Identity etc.)
//
builder.Services.AddInfrastructure(builder.Configuration);

//
// JWT Authentication
//
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)
        ),

        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

//
// CORS CONFIGURATION (configuration-driven)
//
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetValue<string>("Cors:AllowedOrigins")
        ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? new[] { "http://localhost:5173" };

    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
            // If you need cookies or browser credentials, add:
            // .AllowCredentials();
            // but remember AllowCredentials cannot be used with AllowAnyOrigin.
    });
});

//
// Forwarded headers (required when running behind a proxy like Fly)
//
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Allow forwarded headers from any proxy (use carefully in trusted scenarios)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Debug: log the configured CORS origin string so we can confirm the running container read the secret
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var configuredCors = builder.Configuration["Cors:AllowedOrigins"] ?? "<none>";
logger.LogInformation("Configured CORS AllowedOrigins: {Origins}", configuredCors);

// TEMPORARY DEBUG: ensure OPTIONS requests get CORS headers if something is mis-wiring the middleware.
// Keep this only for debugging — remove once root cause is found.
app.Use(async (context, next) =>
{
    if (string.Equals(context.Request.Method, HttpMethods.Options, StringComparison.OrdinalIgnoreCase))
    {
        var origin = context.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin))
        {
            // Echo the Origin if present (temporary, for debugging only)
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            var reqHeaders = context.Request.Headers["Access-Control-Request-Headers"].ToString();
            context.Response.Headers["Access-Control-Allow-Headers"] = string.IsNullOrEmpty(reqHeaders) ? "Content-Type, Authorization" : reqHeaders;
            // If you don't use credentials, do NOT set Allow-Credentials. If you do, set and ensure WithOrigins is not "*".
            // context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        await context.Response.CompleteAsync();
        return;
    }

    await next();
});

app.UseForwardedHeaders();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    // Explicit endpoint and route prefix help when behind a proxy / custom base path
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Classroom.Api v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

//
// Enable CORS
//
app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//
// Auto-migrate + seed roles + seed initial super admin
//
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    foreach (var role in new[] { AppRole.SuperAdmin, AppRole.Teacher, AppRole.Learner })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

    var seedEmail = builder.Configuration["Seed:SuperAdminEmail"];
    var seedPassword = builder.Configuration["Seed:SuperAdminPassword"];
    var seedName = builder.Configuration["Seed:SuperAdminName"] ?? "Super Admin";

    if (!string.IsNullOrWhiteSpace(seedEmail) && !string.IsNullOrWhiteSpace(seedPassword))
    {
        var existing = await userManager.FindByEmailAsync(seedEmail);
        if (existing is null)
        {
            var u = new ApplicationUser
            {
                UserName = seedEmail,
                Email = seedEmail,
                FullName = seedName
            };

            var created = await userManager.CreateAsync(u, seedPassword);
            if (created.Succeeded)
                await userManager.AddToRoleAsync(u, AppRole.SuperAdmin);
        }
    }

    // seed grades and subjects if missing
    if (!await db.Grades.AnyAsync())
    {
        var grades = Enumerable.Range(1, 12).Select(i => new Classroom.Domain.Entities.Grade { Name = $"Grade {i}" }).ToList();
        db.Grades.AddRange(grades);
        await db.SaveChangesAsync();
    }

    if (!await db.Subjects.AnyAsync())
    {
        var subjectNames = new[] { "Mathematics", "English", "Science", "History", "Geography" };
        var grades = await db.Grades.ToListAsync();
        foreach (var g in grades)
        {
            foreach (var sname in subjectNames)
            {
                db.Subjects.Add(new Classroom.Domain.Entities.Subject { Name = sname, GradeId = g.Id });
            }
        }
        await db.SaveChangesAsync();
    }
}

app.Run();      
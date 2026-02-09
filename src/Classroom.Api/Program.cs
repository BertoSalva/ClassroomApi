using Classroom.Domain.Enums;
using Classroom.Infrastructure;
using Classroom.Infrastructure.Identity;
using Classroom.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

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
// CORS CONFIGURATION (Added)
//
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173") // React dev server
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

//
// Enable CORS (Added)
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

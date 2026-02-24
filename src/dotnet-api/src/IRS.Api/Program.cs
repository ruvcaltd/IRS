using IRS.Infrastructure.Data;
using IRS.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using IRS.LLM.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI with NSwag
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "IRS API";
    config.Version = "v1";
    config.Description = "Investment Research System API";
});

// Configure DbContext
builder.Services.AddDbContext<IrsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// Configure Authentication Service
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
var jwtAudience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
var jwtExpiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");

builder.Services.AddScoped<IAuthenticationService>(provider =>
    new AuthenticationService(
        provider.GetRequiredService<IrsDbContext>(),
        jwtKey,
        jwtIssuer,
        jwtAudience,
        jwtExpiryMinutes
    )
);

builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IStructureAgent, StructureAgent>();
builder.Services.AddScoped<IResearchService, ResearchService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<IResearchAgentService, ResearchAgentService>();
builder.Services.AddScoped<ILlmConfigurationService, LlmConfigurationService>();
builder.Services.AddScoped<IRS.LLM.Services.ILlmClientFactory, LlmClientFactory>();
builder.Services.AddHttpClient("openfigi");

// Register LLM services
builder.Services.AddLlmServices(builder.Configuration);

// REST API Agent Runner
builder.Services.AddHostedService<RestApiAgentRunnerHostedService>();

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Forbidden" });
        }
    };
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        var allowedOrigins = new[] 
        { 
            "http://localhost:4200",      // ng serve default
            "http://localhost",            // Docker/production on port 80
            "http://localhost:80",         // Explicit port 80
            "http://angular-ui"            // Docker service name
        };
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

// Global JSON exception handler — ensures unhandled exceptions always return JSON, not HTML
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var message = feature?.Error?.Message ?? "An unexpected error occurred";
        await context.Response.WriteAsJsonAsync(new { error = message });
    });
});

app.UseHttpsRedirection();

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }

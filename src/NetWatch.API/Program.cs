using System.Text;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NetWatch.API.Middleware;
using NetWatch.API.Options;
using NetWatch.API.Services;
using NetWatch.Data;
using NetWatch.Data.Entities;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("NetWatch") ?? throw new InvalidOperationException("ConnectionStrings:NetWatch is required.");
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32) throw new InvalidOperationException("Jwt:Key must contain at least 32 bytes.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(MonitoringOptions.SectionName));
builder.Services.AddDbContext<NetWatchDbContext>(options => options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)), mysql => mysql.EnableRetryOnFailure()));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<DbInitializer>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<DeviceStateService>();
builder.Services.AddScoped<MetricIngestionService>();
if (!builder.Environment.IsEnvironment("Testing")) builder.Services.AddHostedService<PingMonitoringWorker>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var identifier = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(identifier, out var userId) || !await context.HttpContext.RequestServices.GetRequiredService<NetWatchDbContext>().Users.AnyAsync(x => x.Id == userId && x.IsActive))
                context.Fail("User is inactive or no longer exists.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins);
    policy.AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow })).AllowAnonymous();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DbInitializer>().InitializeAsync();
}

app.Run();
public partial class Program;

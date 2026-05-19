using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Konfiguration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Azure App Service port-binding
var port = builder.Configuration["PORT"] ?? Environment.GetEnvironmentVariable("PORT");
bool usesPortBinding = !string.IsNullOrWhiteSpace(port);
if (usesPortBinding)
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// === Add CORS for React web app (Vercel/local dev) ===
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
        policy.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});
// === ===

// Services
builder.Services.AddControllers();
// Generera OpenAPI-dokument (krävs för Scalar)
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration["SUPABASE_CONNECTION_STRING"];
connectionString = NormalizeConnectionString(connectionString);

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string not configured. Set ConnectionStrings:DefaultConnection, DATABASE_URL, or SUPABASE_CONNECTION_STRING.");
}

builder.Services.AddDbContext<AiMusicWorkstationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton(sp => PythonEngineConfig.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient<PythonEngineManager>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PythonEngineConfig>();
    client.BaseAddress = new Uri(cfg.BaseUrl);
    client.Timeout = cfg.Timeout;
});
builder.Services.AddHttpClient<PythonEngineClient>((sp, client) =>
{
    var cfg = sp.GetRequiredService<PythonEngineConfig>();
    client.BaseAddress = new Uri(cfg.BaseUrl);
    client.Timeout = cfg.Timeout;
});

builder.Services.AddScoped<DbLibraryRepository>();
builder.Services.AddScoped<AiMusicWorkstation.Domain.Repositories.ILibraryRepository>(sp =>
    new LibraryRepository(
        sp.GetRequiredService<DbLibraryRepository>(),
        sp.GetRequiredService<ILogger<LibraryRepository>>()
    ));
builder.Services.AddSingleton<SmartImporter>();

var app = builder.Build();

// Apply pending migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AiMusicWorkstationDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to apply migrations on startup. Ensure the database connection string is correct and the database is accessible.");
    }
}

// OpenAPI + Scalar UI (gäller alla miljöer, inte bara Development)
app.MapOpenApi(); // exponerar /openapi/v1.json

// Scalar UI: nås på /scalar/v1
app.MapScalarApiReference(endpointPrefix: "/scalar", options =>
{
    options.Title = "AI Music Workstation API";
    // här kan du lägga till WithTheme, WithDownloadButton etc vid behov
});

// Pipeline
if (!usesPortBinding)
{
    app.UseHttpsRedirection();
}

// === Enable new CORS policy here ===
app.UseCors("AllowWeb");
// === ===

app.UseAuthorization();

app.MapControllers();

// Enkel health‑endpoint för snabb koll att API:t lever
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

static string? NormalizeConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        && !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var connBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            var connHost = connBuilder.Host;
            var connUsername = connBuilder.Username;
            if (!string.IsNullOrEmpty(connHost))
            {
                if (connHost.StartsWith("db.", StringComparison.OrdinalIgnoreCase) && connHost.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = connHost.Split('.');
                    if (parts.Length == 4)
                    {
                        var projectRef = parts[1];
                        connBuilder.Host = "aws-1-eu-north-1.pooler.supabase.com";
                        if (!string.IsNullOrEmpty(connUsername) && !connUsername.EndsWith("." + projectRef, StringComparison.OrdinalIgnoreCase))
                        {
                            connBuilder.Username = $"{connUsername}.{projectRef}";
                        }
                        return connBuilder.ConnectionString;
                    }
                }
            }
        }
        catch
        {
            // Fallback to original connection string if parsing fails
        }
        return connectionString;
    }

    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException(
            "Invalid Postgres URI format in connection string. Verify DATABASE_URL or SUPABASE_CONNECTION_STRING is a properly formatted absolute URI.");
    }

    var userInfo = uri.UserInfo.Split(':', 2);
    var host = uri.Host;
    var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;

    if (host.StartsWith("db.", StringComparison.OrdinalIgnoreCase) && host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
    {
        var parts = host.Split('.');
        if (parts.Length == 4)
        {
            var projectRef = parts[1];
            host = "aws-1-eu-north-1.pooler.supabase.com";
            if (!string.IsNullOrEmpty(username) && !username.EndsWith("." + projectRef, StringComparison.OrdinalIgnoreCase))
            {
                username = $"{username}.{projectRef}";
            }
        }
    }

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = username,
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        SslMode = SslMode.Require
    };

    if (!string.IsNullOrWhiteSpace(uri.Query))
    {
        var parameters = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var parameter in parameters)
        {
            var parts = parameter.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0];
            var value = Uri.UnescapeDataString(parts[1]);
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(value, true, out var sslMode))
            {
                builder.SslMode = sslMode;
            }
        }
    }

    return builder.ConnectionString;
}

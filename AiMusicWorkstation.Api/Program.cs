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

app.MapGet("/test-conn", (IConfiguration config) =>
{
    var defaultConnection = config.GetConnectionString("DefaultConnection");
    var databaseUrl = config["DATABASE_URL"];
    var supabaseString = config["SUPABASE_CONNECTION_STRING"];

    var normDefault = NormalizeConnectionString(defaultConnection);
    var normDatabaseUrl = NormalizeConnectionString(databaseUrl);
    var normSupabase = NormalizeConnectionString(supabaseString);

    Func<string?, string?> maskPassword = (conn) =>
    {
        if (string.IsNullOrEmpty(conn)) return conn;
        try
        {
            var cb = new NpgsqlConnectionStringBuilder(conn);
            if (!string.IsNullOrEmpty(cb.Password)) cb.Password = "***";
            return cb.ConnectionString;
        }
        catch
        {
            if (conn.Contains("@"))
            {
                var parts = conn.Split('@');
                var left = parts[0];
                var right = parts[1];
                if (left.Contains(":"))
                {
                    var leftParts = left.Split(':');
                    var scheme = leftParts[0];
                    var user = leftParts[1].TrimStart('/');
                    return $"{scheme}://{user}:***@{right}";
                }
            }
            return "[Mask Error] " + conn;
        }
    };

    return Results.Ok(new {
        defaultConnection = maskPassword(defaultConnection),
        databaseUrl = maskPassword(databaseUrl),
        supabaseString = maskPassword(supabaseString),
        normDefault = maskPassword(normDefault),
        normDatabaseUrl = maskPassword(normDatabaseUrl),
        normSupabase = maskPassword(normSupabase)
    });
});

app.MapGet("/test-poolers", async (IConfiguration config) =>
{
    var connString = config.GetConnectionString("DefaultConnection")
        ?? config["DATABASE_URL"]
        ?? config["SUPABASE_CONNECTION_STRING"];

    if (string.IsNullOrEmpty(connString))
    {
        return Results.BadRequest("Connection string not configured.");
    }

    NpgsqlConnectionStringBuilder cb;
    string originalHost = "";
    string originalUsername = "";
    try
    {
        if (connString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            connString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(connString, UriKind.Absolute, out var uri))
            {
                var userInfo = uri.UserInfo.Split(':', 2);
                originalHost = uri.Host;
                originalUsername = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
                cb = new NpgsqlConnectionStringBuilder
                {
                    Host = originalHost,
                    Port = uri.Port > 0 ? uri.Port : 5432,
                    Database = uri.AbsolutePath.TrimStart('/'),
                    Username = originalUsername,
                    Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
                    SslMode = SslMode.Require
                };
            }
            else
            {
                return Results.BadRequest("Invalid Postgres URI format in connection string.");
            }
        }
        else
        {
            cb = new NpgsqlConnectionStringBuilder(connString);
            originalHost = cb.Host;
            originalUsername = cb.Username;
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Failed to parse connection string: {ex.Message}");
    }

    var projectRef = "";
    if (originalHost.StartsWith("db.", StringComparison.OrdinalIgnoreCase) && originalHost.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
    {
        var parts = originalHost.Split('.');
        if (parts.Length == 4)
        {
            projectRef = parts[1];
        }
    }

    if (string.IsNullOrEmpty(projectRef))
    {
        return Results.BadRequest($"Could not extract project reference from host: {originalHost}");
    }

    var regions = new[]
    {
        "eu-north-1",     // Stockholm
        "eu-central-1",   // Frankfurt
        "eu-west-1",      // Ireland
        "eu-west-2",      // London
        "eu-west-3",      // Paris
        "us-east-1",      // N. Virginia
        "us-east-2",      // Ohio
        "us-west-1",      // N. California
        "us-west-2",      // Oregon
        "ap-southeast-1", // Singapore
        "ap-northeast-1", // Tokyo
        "ap-northeast-2", // Seoul
        "sa-east-1"       // São Paulo
    };

    var results = new System.Collections.Generic.List<object>();

    foreach (var region in regions)
    {
        foreach (var prefix in new[] { "aws-0", "aws-1" })
        {
            var poolerHost = $"{prefix}-{region}.pooler.supabase.com";
            var testCb = new NpgsqlConnectionStringBuilder(cb.ConnectionString)
            {
                Host = poolerHost,
                Username = originalUsername.EndsWith("." + projectRef, StringComparison.OrdinalIgnoreCase) 
                    ? originalUsername 
                    : $"{originalUsername}.{projectRef}"
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var conn = new NpgsqlConnection(testCb.ConnectionString);
                await conn.OpenAsync();
                stopwatch.Stop();
                results.Add(new { region, host = poolerHost, success = true, timeMs = stopwatch.ElapsedMilliseconds, message = "Success" });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                results.Add(new { region, host = poolerHost, success = false, timeMs = stopwatch.ElapsedMilliseconds, message = ex.Message });
            }
        }
    }

    return Results.Ok(results);
});

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
                        connBuilder.Host = "aws-0-eu-north-1.pooler.supabase.com";
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
            host = "aws-0-eu-north-1.pooler.supabase.com";
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

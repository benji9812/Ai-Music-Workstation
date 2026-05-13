using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

var port = builder.Configuration["PORT"] ?? Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration["SUPABASE_CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string not configured. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

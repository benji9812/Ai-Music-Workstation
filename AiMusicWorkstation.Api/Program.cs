using AiMusicWorkstation.Infrastructure.ExternalServices;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

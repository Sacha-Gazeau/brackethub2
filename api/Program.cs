using Supabase;
using api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
var supabaseKey = builder.Configuration["Supabase:ServiceKey"]
    ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");
var frontendOrigins = new[]
{
    "https://brackethub-seven.vercel.app",
    "http://localhost:5173",
    "https://localhost:5173",
    "http://127.0.0.1:5173",
    "https://127.0.0.1:5173"
    
};

if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
{
    throw new Exception($"Supabase configuration is missing. Environment: {builder.Environment.EnvironmentName}. Check user-secrets for Supabase:Url and Supabase:ServiceKey.");
}

builder.Services.AddSingleton(new Client(supabaseUrl, supabaseKey));
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<DailyRewardService>();
builder.Services.AddScoped<BettingService>();
builder.Services.AddScoped<TeamRequestService>();
builder.Services.AddScoped<TournamentStageService>();
builder.Services.AddHttpClient<IDiscordService, DiscordService>();
builder.Services.AddHostedService<TournamentReminderBackgroundService>();
builder.Services.AddHttpClient<IgdbService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors("Frontend");
app.MapControllers();

app.Run();

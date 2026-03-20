using Discord;
using Discord.WebSocket;
using Supabase;
using api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);

var resolvedConfiguration = new Dictionary<string, string?>
{
    ["Supabase:Url"] = GetConfigValue(builder.Configuration, "Supabase:Url", "SUPABASE_URL"),
    ["Supabase:ServiceKey"] = GetConfigValue(builder.Configuration, "Supabase:ServiceKey", "SUPABASE_SERVICE_KEY"),
    ["Supabase:PublishableKey"] = GetConfigValue(builder.Configuration, "Supabase:PublishableKey", "SUPABASE_PUBLISHABLE_KEY"),
    ["Supabase:JwtSecret"] = GetConfigValue(builder.Configuration, "Supabase:JwtSecret", "SUPABASE_JWT_SECRET"),
    ["IGDB:ClientId"] = GetConfigValue(builder.Configuration, "IGDB:ClientId", "IGDB_CLIENT_ID"),
    ["IGDB:ClientSecret"] = GetConfigValue(builder.Configuration, "IGDB:ClientSecret", "IGDB_CLIENT_SECRET"),
    ["Discord:BotToken"] = GetConfigValue(builder.Configuration, "Discord:BotToken", "DISCORD_BOT_TOKEN"),
    ["Discord:GuildId"] = GetConfigValue(builder.Configuration, "Discord:GuildId", "DISCORD_GUILD_ID"),
    ["App:PublicBaseUrl"] = GetConfigValue(builder.Configuration, "App:PublicBaseUrl", "APP_PUBLIC_BASE_URL"),
};

builder.Configuration.AddInMemoryCollection(resolvedConfiguration);

var supabaseUrl = resolvedConfiguration["Supabase:Url"]!;
var supabaseKey = resolvedConfiguration["Supabase:ServiceKey"]!;

var frontendOrigins = new[]
{
    "https://brackethub-seven.vercel.app",
    "http://localhost:5173",
    "https://localhost:5173",
    "http://127.0.0.1:5173",
    "https://127.0.0.1:5173"
    
};

builder.Services.AddSingleton(new Client(supabaseUrl, supabaseKey));
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.DirectMessages,
    LogGatewayIntentWarnings = false
}));
builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
builder.Services.AddScoped<DailyRewardService>();
builder.Services.AddScoped<BettingService>();
builder.Services.AddScoped<TeamRequestService>();
builder.Services.AddScoped<TournamentStageService>();
builder.Services.AddSingleton<IDiscordService, DiscordService>();
builder.Services.AddHostedService<DiscordBotHostedService>();
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

static string GetConfigValue(
    IConfiguration configuration,
    string configKey,
    string environmentVariableName)
{
    var value = Environment.GetEnvironmentVariable(environmentVariableName);
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    value = configuration[configKey];
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    throw new InvalidOperationException(
        $"Missing required configuration value '{configKey}' (fallback environment variable '{environmentVariableName}').");
}

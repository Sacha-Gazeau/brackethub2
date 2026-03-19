using Discord;
using Discord.WebSocket;

namespace api.Services;

public sealed class DiscordBotHostedService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordBotHostedService> _logger;

    public DiscordBotHostedService(
        DiscordSocketClient client,
        IConfiguration configuration,
        ILogger<DiscordBotHostedService> logger)
    {
        _client = client;
        _configuration = configuration;
        _logger = logger;

        _client.Log += OnDiscordLogAsync;
        _client.Ready += OnReadyAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var botToken = _configuration["Discord:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException(
                "Missing required configuration value 'Discord:BotToken'. Configure it with .NET user-secrets.");
        }

        await _client.LoginAsync(TokenType.Bot, botToken);
        await _client.StartAsync();

        _logger.LogInformation("Discord bot login started.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Ready -= OnReadyAsync;
        _client.Log -= OnDiscordLogAsync;

        if (_client.ConnectionState != ConnectionState.Disconnected)
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private Task OnReadyAsync()
    {
        _logger.LogInformation("Discord bot connected.");
        Console.WriteLine("Discord bot connected");
        return Task.CompletedTask;
    }

    private Task OnDiscordLogAsync(LogMessage logMessage)
    {
        var level = logMessage.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        _logger.Log(level, logMessage.Exception, "[Discord] {Message}", logMessage.Message);
        return Task.CompletedTask;
    }
}

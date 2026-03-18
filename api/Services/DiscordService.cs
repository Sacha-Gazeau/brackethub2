using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace api.Services;

public class DiscordService : IDiscordService
{
    private const string DefaultApiBaseUrl = "https://discord.com/api/v10/";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordService> _logger;

    public DiscordService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DiscordService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var apiBaseUrl = _configuration["Discord:ApiBaseUrl"];
        _httpClient.BaseAddress = new Uri(
            string.IsNullOrWhiteSpace(apiBaseUrl) ? DefaultApiBaseUrl : apiBaseUrl);
    }

    public async Task<string?> CreateDmChannelAsync(
        string discordId,
        CancellationToken cancellationToken = default)
    {
        if (!TryCreateAuthorizedRequest(
                HttpMethod.Post,
                "users/@me/channels",
                out var request,
                JsonSerializer.Serialize(new { recipient_id = discordId })))
        {
            return null;
        }

        using (request)
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogDiscordFailureAsync("create DM channel", discordId, response, cancellationToken);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return payload.RootElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;
        }
    }

    public async Task<bool> SendDmAsync(
        string discordId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(discordId) || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        try
        {
            var dmChannelId = await CreateDmChannelAsync(discordId, cancellationToken);
            if (string.IsNullOrWhiteSpace(dmChannelId))
            {
                return false;
            }

            if (!TryCreateAuthorizedRequest(
                    HttpMethod.Post,
                    $"channels/{dmChannelId}/messages",
                    out var request,
                    JsonSerializer.Serialize(new { content = message })))
            {
                return false;
            }

            using (request)
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                await LogDiscordFailureAsync("send DM", discordId, response, cancellationToken);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discord DM send failed for Discord user {DiscordId}.", discordId);
            return false;
        }
    }

    public Task<bool> SendWelcomeMessageAsync(
        string discordId,
        string username,
        CancellationToken cancellationToken = default) =>
        SendDmAsync(
            discordId,
            $"Welkom op BracketHub, {username}!{Environment.NewLine}{Environment.NewLine}" +
            "Je account is succesvol aangemaakt." + Environment.NewLine +
            "Je kan nu deelnemen aan toernooien, coins verdienen en rewards inwisselen.",
            cancellationToken);

    public Task<bool> SendJoinRequestNotificationAsync(
        string organizerDiscordId,
        string teamName,
        string captainName,
        string adminTeamsUrl,
        CancellationToken cancellationToken = default) =>
        SendDmAsync(
            organizerDiscordId,
            "Nieuwe deelnamerequest voor je toernooi" + Environment.NewLine + Environment.NewLine +
            $"Team: {teamName}" + Environment.NewLine +
            $"Kapitein: {captainName}" + Environment.NewLine + Environment.NewLine +
            "Beheer de aanvraag hier:" + Environment.NewLine +
            adminTeamsUrl,
            cancellationToken);

    public Task<bool> SendJoinRequestResponseAsync(
        string userDiscordId,
        bool accepted,
        string tournamentName,
        DateTime startDate,
        string? rejectionReason,
        CancellationToken cancellationToken = default)
    {
        var message = accepted
            ? "Goed nieuws! ✅" + Environment.NewLine +
              $"Je team is geaccepteerd voor het toernooi: {tournamentName}" + Environment.NewLine + Environment.NewLine +
              $"Startdatum: {startDate:dd/MM/yyyy HH:mm}" + Environment.NewLine +
              "Veel succes!"
            : $"Je aanvraag voor het toernooi {tournamentName} is geweigerd. ❌" + Environment.NewLine + Environment.NewLine +
              "Reden:" + Environment.NewLine +
              (string.IsNullOrWhiteSpace(rejectionReason) ? "Geen reden opgegeven." : rejectionReason);

        return SendDmAsync(userDiscordId, message, cancellationToken);
    }

    public Task<bool> SendTournamentReminderAsync(
        string userDiscordId,
        string tournamentName,
        string tournamentUrl,
        CancellationToken cancellationToken = default) =>
        SendDmAsync(
            userDiscordId,
            "Herinnering ⏰" + Environment.NewLine +
            $"Je neemt deel aan {tournamentName}." + Environment.NewLine +
            "Het toernooi start binnen 1 uur." + Environment.NewLine + Environment.NewLine +
            "Bekijk het toernooi hier:" + Environment.NewLine +
            tournamentUrl,
            cancellationToken);

    public Task<bool> SendBetResultAsync(
        string userDiscordId,
        bool won,
        string matchResult,
        int coinsAmount,
        CancellationToken cancellationToken = default)
    {
        var message = won
            ? "Je hebt je pari gewonnen! 🎉" + Environment.NewLine + Environment.NewLine +
              "Resultaat:" + Environment.NewLine +
              $"{matchResult}" + Environment.NewLine + Environment.NewLine +
              $"Je hebt {coinsAmount} coins gewonnen."
            : "Je hebt je pari verloren. 😢" + Environment.NewLine + Environment.NewLine +
              "Resultaat:" + Environment.NewLine +
              $"{matchResult}" + Environment.NewLine + Environment.NewLine +
              $"Je hebt {coinsAmount} coins verloren.";

        return SendDmAsync(userDiscordId, message, cancellationToken);
    }

    public Task<bool> SendRewardDeliveryAsync(
        string userDiscordId,
        string rewardName,
        string rewardCode,
        CancellationToken cancellationToken = default) =>
        SendDmAsync(
            userDiscordId,
            "Je reward is klaar! 🎁" + Environment.NewLine + Environment.NewLine +
            $"Reward: {rewardName}" + Environment.NewLine + Environment.NewLine +
            "Hier is je code:" + Environment.NewLine +
            rewardCode,
            cancellationToken);

    private bool TryCreateAuthorizedRequest(
        HttpMethod method,
        string relativeUrl,
        out HttpRequestMessage request,
        string? jsonBody = null)
    {
        request = new HttpRequestMessage(method, relativeUrl);
        var botToken = _configuration["Discord:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogWarning("Discord:BotToken is missing. Discord notifications are disabled.");
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        if (jsonBody != null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        return true;
    }

    private async Task LogDiscordFailureAsync(
        string action,
        string discordId,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Discord API failed to {Action} for Discord user {DiscordId}. Status: {StatusCode}. Response: {ResponseBody}",
            action,
            discordId,
            (int)response.StatusCode,
            body);
    }
}

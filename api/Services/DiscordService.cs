using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace api.Services;

public class DiscordService : IDiscordService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordService> _logger;
    private readonly ulong _guildId;

    public DiscordService(
        DiscordSocketClient client,
        IConfiguration configuration,
        ILogger<DiscordService> logger)
    {
        _client = client;
        _logger = logger;
        _guildId = ulong.TryParse(configuration["Discord:GuildId"], out var configuredGuildId)
            ? configuredGuildId
            : 1483998783149834260;
    }

    public async Task<bool> SendPrivateMessageAsync(
        ulong userId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (userId == 0 || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        try
        {
            IUser? user = _client.GetUser(userId);
            if (user is null)
            {
                _logger.LogWarning("Discord user {UserId} was not found in cache. Falling back to REST.", userId);
                user = await _client.Rest.GetUserAsync(userId);
            }

            if (user is null)
            {
                _logger.LogWarning("Discord user {UserId} does not exist or is unreachable.", userId);
                return false;
            }

            var dmChannel = await user.CreateDMChannelAsync(new RequestOptions
            {
                CancelToken = cancellationToken
            });

            await dmChannel.SendMessageAsync(message, options: new RequestOptions
            {
                CancelToken = cancellationToken
            });

            return true;
        }
        catch (Exception ex) when (ex is HttpException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Discord DM send failed for Discord user {UserId}.", userId);
            return false;
        }
    }

    public async Task<bool> IsUserInGuildAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        var result = await CheckGuildMembershipAsync(userId, cancellationToken);
        return result.BotReady && result.IsInGuild;
    }

    public async Task<DiscordGuildCheckResult> CheckGuildMembershipAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == 0)
        {
            return new DiscordGuildCheckResult(false, false, _guildId);
        }

        var guild = _client.GetGuild(_guildId);
        if (guild is null)
        {
            _logger.LogError(
                "Discord guild {GuildId} was not found. Returning false for membership check because the bot is not ready or not invited.",
                _guildId);
            return new DiscordGuildCheckResult(false, false, _guildId);
        }

        var guildUser = guild.GetUser(userId);
        if (guildUser is not null)
        {
            _logger.LogInformation("User found in guild. GuildId: {GuildId}, UserId: {UserId}", _guildId, userId);
            return new DiscordGuildCheckResult(true, true, _guildId);
        }

        try
        {
            var restGuildUser = await _client.Rest.GetGuildUserAsync(_guildId, userId, new RequestOptions
            {
                CancelToken = cancellationToken
            });

            guildUser = restGuildUser is null ? null : guild.GetUser(restGuildUser.Id);
            if (guildUser is null && restGuildUser is not null)
            {
                _logger.LogInformation(
                    "User found in guild via REST. GuildId: {GuildId}, UserId: {UserId}",
                    _guildId,
                    userId);
                return new DiscordGuildCheckResult(true, true, _guildId);
            }
        }
        catch (HttpException ex) when ((int)ex.HttpCode == 404)
        {
            _logger.LogInformation("User not in guild. GuildId: {GuildId}, UserId: {UserId}", _guildId, userId);
            return new DiscordGuildCheckResult(true, false, _guildId);
        }
        catch (Exception ex) when (ex is HttpException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "Failed to verify guild membership. Returning false. GuildId: {GuildId}, UserId: {UserId}",
                _guildId,
                userId);
            return new DiscordGuildCheckResult(false, false, _guildId);
        }

        var isInGuild = guildUser is not null;
        if (isInGuild)
        {
            _logger.LogInformation("User found in guild. GuildId: {GuildId}, UserId: {UserId}", _guildId, userId);
        }
        else
        {
            _logger.LogInformation("User not in guild. GuildId: {GuildId}, UserId: {UserId}", _guildId, userId);
        }

        return new DiscordGuildCheckResult(true, isInGuild, _guildId);
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

        if (!ulong.TryParse(discordId, out var userId))
        {
            _logger.LogWarning("Discord ID '{DiscordId}' is invalid.", discordId);
            return false;
        }

        return await SendPrivateMessageAsync(userId, message, cancellationToken);
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

}

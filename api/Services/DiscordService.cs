using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace api.Services;

public class DiscordService : IDiscordService
{
    private readonly DiscordSocketClient _client;
    private readonly IAppTextService _text;
    private readonly ILogger<DiscordService> _logger;
    private readonly ulong _guildId;

    public DiscordService(
        DiscordSocketClient client,
        IConfiguration configuration,
        IAppTextService text,
        ILogger<DiscordService> logger)
    {
        _client = client;
        _text = text;
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
        var result = await SendPrivateMessageCoreAsync(userId, message, cancellationToken);
        return result.Success;
    }

    public async Task<DiscordActionResult> SendDirectMessageAsync(
        string discordUserId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(discordUserId))
        {
            return CreateFailure("discord_user_id_missing", "backendMessages.discord.errors.userIdMissing");
        }

        if (!ulong.TryParse(discordUserId, out var userId))
        {
            _logger.LogWarning("Discord ID '{DiscordId}' is invalid.", discordUserId);
            return CreateFailure("discord_user_id_invalid", "backendMessages.discord.errors.userIdInvalid");
        }

        return await SendPrivateMessageCoreAsync(userId, message, cancellationToken);
    }

    public async Task<DiscordActionResult> AddRoleToUserAsync(
        string discordUserId,
        string discordRoleId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(discordUserId))
        {
            return CreateFailure("discord_user_id_missing", "backendMessages.discord.errors.userIdMissing");
        }

        if (string.IsNullOrWhiteSpace(discordRoleId))
        {
            return CreateFailure("discord_role_id_missing", "backendMessages.discord.errors.roleIdMissing");
        }

        if (!ulong.TryParse(discordUserId, out var userId))
        {
            _logger.LogWarning("Discord ID '{DiscordId}' is invalid.", discordUserId);
            return CreateFailure("discord_user_id_invalid", "backendMessages.discord.errors.userIdInvalid");
        }

        if (!ulong.TryParse(discordRoleId, out var roleId))
        {
            _logger.LogWarning("Discord role ID '{DiscordRoleId}' is invalid.", discordRoleId);
            return CreateFailure("discord_role_id_invalid", "backendMessages.discord.errors.roleIdInvalid");
        }

        try
        {
            var guildUserResult = await ResolveGuildUserAsync(userId, cancellationToken);
            if (guildUserResult.User is null)
            {
                return guildUserResult.Failure
                    ?? CreateFailure("discord_user_not_in_guild", "backendMessages.discord.errors.userNotInGuild");
            }

            await guildUserResult.User.AddRoleAsync(roleId, new RequestOptions
            {
                CancelToken = cancellationToken
            });

            return new DiscordActionResult(true, null, null);
        }
        catch (HttpException ex) when ((int)ex.HttpCode == 403)
        {
            _logger.LogWarning(
                ex,
                "Discord role assignment forbidden. GuildId: {GuildId}, UserId: {UserId}",
                _guildId,
                userId);
            return CreateFailure("discord_missing_permissions", "backendMessages.discord.errors.missingPermissions");
        }
        catch (HttpException ex) when ((int)ex.HttpCode == 404)
        {
            _logger.LogWarning(
                ex,
                "Discord role or guild member was not found. GuildId: {GuildId}, UserId: {UserId}, RoleId: {RoleId}",
                _guildId,
                userId,
                roleId);
            return CreateFailure("discord_role_not_found", "backendMessages.discord.errors.roleNotFound");
        }
        catch (Exception ex) when (ex is HttpException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "Failed to assign Discord role. GuildId: {GuildId}, UserId: {UserId}, RoleId: {RoleId}",
                _guildId,
                userId,
                roleId);
            return CreateFailure("discord_role_assignment_failed", "backendMessages.discord.errors.roleAssignmentFailed");
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
        var result = await SendDirectMessageAsync(discordId, message, cancellationToken);
        return result.Success;
    }

    public Task<bool> SendWelcomeMessageAsync(
        string discordId,
        string username,
        CancellationToken cancellationToken = default) =>
        SendDmAsync(
            discordId,
            _text.Get("discordMessages.welcome", new Dictionary<string, string?>
            {
                ["username"] = username
            }),
            cancellationToken);

    public Task<bool> SendJoinRequestNotificationAsync(
        string organizerDiscordId,
        string teamName,
        string captainName,
        string adminTeamsUrl,
        CancellationToken cancellationToken = default) =>
        SendDmAsync(
            organizerDiscordId,
            _text.Get("discordMessages.joinRequestNotification", new Dictionary<string, string?>
            {
                ["teamName"] = teamName,
                ["captainName"] = captainName,
                ["adminTeamsUrl"] = adminTeamsUrl
            }),
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
            ? _text.Get("discordMessages.joinRequestAccepted", new Dictionary<string, string?>
            {
                ["tournamentName"] = tournamentName,
                ["startDate"] = startDate.ToString("dd/MM/yyyy HH:mm")
            })
            : _text.Get("discordMessages.joinRequestRejected", new Dictionary<string, string?>
            {
                ["tournamentName"] = tournamentName,
                ["reason"] = string.IsNullOrWhiteSpace(rejectionReason)
                    ? _text.Get("discordMessages.joinRequestRejectedNoReason")
                    : rejectionReason
            });

        return SendDmAsync(userDiscordId, message, cancellationToken);
    }

    public Task<bool> SendTournamentReminderAsync(
        string userDiscordId,
        string tournamentName,
        string tournamentUrl,
        CancellationToken cancellationToken = default) =>
        SendDmAsync(
            userDiscordId,
            _text.Get("discordMessages.tournamentReminder", new Dictionary<string, string?>
            {
                ["tournamentName"] = tournamentName,
                ["tournamentUrl"] = tournamentUrl
            }),
            cancellationToken);

    public Task<bool> SendBetResultAsync(
        string userDiscordId,
        bool won,
        string matchResult,
        int coinsAmount,
        CancellationToken cancellationToken = default)
    {
        var message = won
            ? _text.Get("discordMessages.betResultWon", new Dictionary<string, string?>
            {
                ["matchResult"] = matchResult,
                ["coinsAmount"] = coinsAmount.ToString()
            })
            : _text.Get("discordMessages.betResultLost", new Dictionary<string, string?>
            {
                ["matchResult"] = matchResult,
                ["coinsAmount"] = coinsAmount.ToString()
            });

        return SendDmAsync(userDiscordId, message, cancellationToken);
    }

    public Task<bool> SendRewardDeliveryAsync(
        string userDiscordId,
        string rewardName,
        string rewardCode,
        CancellationToken cancellationToken = default) =>
        SendDmAsync(
            userDiscordId,
            _text.Get("discordMessages.rewardDelivery", new Dictionary<string, string?>
            {
                ["rewardName"] = rewardName,
                ["rewardCode"] = rewardCode
            }),
            cancellationToken);
    
    private async Task<DiscordActionResult> SendPrivateMessageCoreAsync(
        ulong userId,
        string message,
        CancellationToken cancellationToken)
    {
        if (userId == 0)
        {
            return CreateFailure("discord_user_id_invalid", "backendMessages.discord.errors.userIdInvalid");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return CreateFailure("discord_message_empty", "backendMessages.discord.errors.messageEmpty");
        }

        try
        {
            var user = await ResolveDiscordUserAsync(userId);
            if (user is null)
            {
                _logger.LogWarning("Discord user {UserId} does not exist or is unreachable.", userId);
                return CreateFailure("discord_user_not_found", "backendMessages.discord.errors.userNotFound");
            }

            var dmChannel = await user.CreateDMChannelAsync(new RequestOptions
            {
                CancelToken = cancellationToken
            });

            await dmChannel.SendMessageAsync(message, options: new RequestOptions
            {
                CancelToken = cancellationToken
            });

            return new DiscordActionResult(true, null, null);
        }
        catch (Exception ex) when (ex is HttpException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Discord DM send failed for Discord user {UserId}.", userId);
            return CreateFailure("discord_dm_failed", "backendMessages.discord.errors.dmFailed");
        }
    }

    private async Task<IUser?> ResolveDiscordUserAsync(ulong userId)
    {
        var user = _client.GetUser(userId);
        if (user is not null)
        {
            return user;
        }

        _logger.LogWarning("Discord user {UserId} was not found in cache. Falling back to REST.", userId);
        return await _client.Rest.GetUserAsync(userId);
    }

    private async Task<(IGuildUser? User, DiscordActionResult? Failure)> ResolveGuildUserAsync(
        ulong userId,
        CancellationToken cancellationToken)
    {
        var guild = _client.GetGuild(_guildId);
        if (guild is null)
        {
            _logger.LogError(
                "Discord guild {GuildId} was not found while assigning a role.",
                _guildId);
            return (null, CreateFailure("discord_bot_not_ready", "backendMessages.discord.errors.botNotReady"));
        }

        var guildUser = guild.GetUser(userId);
        if (guildUser is not null)
        {
            return (guildUser, null);
        }

        try
        {
            var restGuildUser = await _client.Rest.GetGuildUserAsync(_guildId, userId, new RequestOptions
            {
                CancelToken = cancellationToken
            });

            return restGuildUser is null
                ? (null, CreateFailure("discord_user_not_in_guild", "backendMessages.discord.errors.userNotInGuild"))
                : (restGuildUser, null);
        }
        catch (HttpException ex) when ((int)ex.HttpCode == 404)
        {
            _logger.LogInformation("User not in guild. GuildId: {GuildId}, UserId: {UserId}", _guildId, userId);
            return (null, CreateFailure("discord_user_not_in_guild", "backendMessages.discord.errors.userNotInGuild"));
        }
        catch (Exception ex) when (ex is HttpException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve guild user. GuildId: {GuildId}, UserId: {UserId}",
                _guildId,
                userId);
            return (null, CreateFailure("discord_user_lookup_failed", "backendMessages.discord.errors.userLookupFailed"));
        }
    }

    private DiscordActionResult CreateFailure(string errorCode, string textKey) =>
        new(false, errorCode, _text.Get(textKey));
}

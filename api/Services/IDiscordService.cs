namespace api.Services;

public interface IDiscordService
{
    Task<bool> SendPrivateMessageAsync(ulong userId, string message, CancellationToken cancellationToken = default);
    Task<bool> IsUserInGuildAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<bool> SendDmAsync(string discordId, string message, CancellationToken cancellationToken = default);
    Task<bool> SendWelcomeMessageAsync(string discordId, string username, CancellationToken cancellationToken = default);
    Task<bool> SendJoinRequestNotificationAsync(string organizerDiscordId, string teamName, string captainName, string adminTeamsUrl, CancellationToken cancellationToken = default);
    Task<bool> SendJoinRequestResponseAsync(string userDiscordId, bool accepted, string tournamentName, DateTime startDate, string? rejectionReason, CancellationToken cancellationToken = default);
    Task<bool> SendTournamentReminderAsync(string userDiscordId, string tournamentName, string tournamentUrl, CancellationToken cancellationToken = default);
    Task<bool> SendBetResultAsync(string userDiscordId, bool won, string matchResult, int coinsAmount, CancellationToken cancellationToken = default);
    Task<bool> SendRewardDeliveryAsync(string userDiscordId, string rewardName, string rewardCode, CancellationToken cancellationToken = default);
}

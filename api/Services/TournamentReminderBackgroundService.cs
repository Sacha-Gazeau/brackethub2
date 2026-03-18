using api.Models;
using PostgrestOperator = Supabase.Postgrest.Constants.Operator;
using PostgrestQueryOptions = Supabase.Postgrest.QueryOptions;
using SupabaseClient = Supabase.Client;

namespace api.Services;

public class TournamentReminderBackgroundService : BackgroundService
{
    private const string ReminderType = "tournament_reminder_1h";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TournamentReminderBackgroundService> _logger;

    public TournamentReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<TournamentReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReminderWindowAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tournament reminder background service failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessReminderWindowAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var supabase = scope.ServiceProvider.GetRequiredService<SupabaseClient>();
        var discordService = scope.ServiceProvider.GetRequiredService<IDiscordService>();

        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(55);
        var windowEnd = now.AddMinutes(65);

        var tournamentsResponse = await supabase
            .From<TournamentInsert>()
            .Select("*")
            .Filter("status", PostgrestOperator.Equals, "aankomend")
            .Filter("start_date", PostgrestOperator.GreaterThanOrEqual, windowStart.ToString("O"))
            .Filter("start_date", PostgrestOperator.LessThanOrEqual, windowEnd.ToString("O"))
            .Get();

        foreach (var tournament in tournamentsResponse.Models)
        {
            var teamsResponse = await supabase
                .From<Team>()
                .Select("*")
                .Filter("tournament_id", PostgrestOperator.Equals, tournament.Id.ToString())
                .Filter("status", PostgrestOperator.Equals, "accepted")
                .Get();

            foreach (var team in teamsResponse.Models)
            {
                var captain = await GetProfileAsync(supabase, team.CaptainId);
                if (captain?.DiscordId == null)
                {
                    continue;
                }

                var referenceKey = $"tournament:{tournament.Id}:start:{tournament.StartDate:O}";
                if (await NotificationAlreadyHandledAsync(supabase, team.CaptainId, referenceKey, cancellationToken))
                {
                    continue;
                }

                var success = await discordService.SendTournamentReminderAsync(
                    captain.DiscordId,
                    tournament.Name,
                    BuildPublicUrl($"/tournament/{tournament.Slug}"),
                    cancellationToken);

                await RecordNotificationAttemptAsync(
                    supabase,
                    team.CaptainId,
                    referenceKey,
                    success,
                    cancellationToken);
            }
        }
    }

    private static async Task<Profile?> GetProfileAsync(SupabaseClient supabase, Guid userId)
    {
        var response = await supabase
            .From<Profile>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, userId.ToString())
            .Limit(1)
            .Get();

        return response.Models.FirstOrDefault();
    }

    private static async Task<bool> NotificationAlreadyHandledAsync(
        SupabaseClient supabase,
        Guid userId,
        string referenceKey,
        CancellationToken cancellationToken)
    {
        var response = await supabase
            .From<NotificationDelivery>()
            .Select("id")
            .Filter("notification_type", PostgrestOperator.Equals, ReminderType)
            .Filter("user_id", PostgrestOperator.Equals, userId.ToString())
            .Filter("reference_key", PostgrestOperator.Equals, referenceKey)
            .Limit(1)
            .Get(cancellationToken);

        return response.Models.Count > 0;
    }

    private static async Task RecordNotificationAttemptAsync(
        SupabaseClient supabase,
        Guid userId,
        string referenceKey,
        bool success,
        CancellationToken cancellationToken)
    {
        await supabase
            .From<NotificationDelivery>()
            .Insert(new NotificationDelivery
            {
                NotificationType = ReminderType,
                UserId = userId,
                ReferenceKey = referenceKey,
                Success = success,
                CreatedAt = DateTime.UtcNow
            }, new PostgrestQueryOptions(), cancellationToken);
    }

    private string BuildPublicUrl(string relativePath)
    {
        var baseUrl = _configuration["App:PublicBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? relativePath
            : $"{baseUrl}{relativePath}";
    }
}

using api.Models;
using PostgrestOperator = Supabase.Postgrest.Constants.Operator;
using PostgrestQueryOptions = Supabase.Postgrest.QueryOptions;
using SupabaseClient = Supabase.Client;

namespace api.Services;

public enum CreateTeamRequestStatus
{
    Success,
    TournamentNotFound,
    TournamentFull,
    PendingRequestAlreadyExists,
    RejectionLimitReached,
    InvalidPlayerCount
}

public sealed class CreateTeamRequestCommand
{
    public long TournamentId { get; init; }
    public Guid CaptainId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> PlayerNames { get; init; } = Array.Empty<string>();
}

public sealed class CreateTeamRequestResult
{
    public CreateTeamRequestStatus Status { get; init; }
    public Team? Team { get; init; }
}

public enum CreateAdminTeamStatus
{
    Success,
    TournamentNotFound,
    TournamentFull,
    InvalidPlayerCount
}

public sealed class CreateAdminTeamCommand
{
    public long TournamentId { get; init; }
    public Guid CaptainId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> PlayerNames { get; init; } = Array.Empty<string>();
}

public sealed class CreateAdminTeamResult
{
    public CreateAdminTeamStatus Status { get; init; }
    public Team? Team { get; init; }
}

public enum ReviewTeamRequestStatus
{
    Success,
    TeamNotFound,
    TournamentNotFound,
    RejectionReasonRequired,
    TournamentFull
}

public sealed class ReviewTeamRequestCommand
{
    public long TeamId { get; init; }
    public TeamStatus Status { get; init; }
    public string? RejectionReason { get; init; }
}

public sealed class ReviewTeamRequestResult
{
    public ReviewTeamRequestStatus Status { get; init; }
    public Team? Team { get; init; }
    public int? CurrentTeams { get; init; }
    public TournamentTeamStatus? TournamentTeamStatus { get; init; }
}

public class TeamRequestService
{
    private readonly SupabaseClient _supabase;
    private readonly IDiscordService _discordService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TeamRequestService> _logger;

    public TeamRequestService(
        SupabaseClient supabase,
        IDiscordService discordService,
        IConfiguration configuration,
        ILogger<TeamRequestService> logger)
    {
        _supabase = supabase;
        _discordService = discordService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CreateTeamRequestResult> CreateAsync(CreateTeamRequestCommand command)
    {
        var tournament = await GetTournamentAsync(command.TournamentId);
        if (tournament == null)
        {
            return new CreateTeamRequestResult
            {
                Status = CreateTeamRequestStatus.TournamentNotFound
            };
        }

        if (tournament.CurrentTeams >= tournament.MaxTeams)
        {
            return new CreateTeamRequestResult
            {
                Status = CreateTeamRequestStatus.TournamentFull
            };
        }

        var pendingRequestResponse = await _supabase
            .From<Team>()
            .Select("id")
            .Filter("tournament_id", PostgrestOperator.Equals, command.TournamentId.ToString())
            .Filter("captain_id", PostgrestOperator.Equals, command.CaptainId.ToString())
            .Filter("status", PostgrestOperator.Equals, "pending")
            .Limit(1)
            .Get();

        if (pendingRequestResponse.Models.Count > 0)
        {
            return new CreateTeamRequestResult
            {
                Status = CreateTeamRequestStatus.PendingRequestAlreadyExists
            };
        }

        var rejectedRequestsResponse = await _supabase
            .From<Team>()
            .Select("id")
            .Filter("tournament_id", PostgrestOperator.Equals, command.TournamentId.ToString())
            .Filter("captain_id", PostgrestOperator.Equals, command.CaptainId.ToString())
            .Filter("status", PostgrestOperator.Equals, "rejected")
            .Limit(2)
            .Get();

        if (rejectedRequestsResponse.Models.Count >= 2)
        {
            return new CreateTeamRequestResult
            {
                Status = CreateTeamRequestStatus.RejectionLimitReached
            };
        }

        if (command.PlayerNames.Count != tournament.PlayersPerTeam)
        {
            return new CreateTeamRequestResult
            {
                Status = CreateTeamRequestStatus.InvalidPlayerCount
            };
        }

        var createdTeam = await CreateTeamAsync(
            tournament,
            command.TeamName,
            command.CaptainId,
            command.PlayerNames,
            TeamStatus.Pending);

        return new CreateTeamRequestResult
        {
            Status = CreateTeamRequestStatus.Success,
            Team = createdTeam
        };
    }

    public async Task<CreateAdminTeamResult> CreateAdminAsync(CreateAdminTeamCommand command)
    {
        var tournament = await GetTournamentAsync(command.TournamentId);
        if (tournament == null)
        {
            return new CreateAdminTeamResult
            {
                Status = CreateAdminTeamStatus.TournamentNotFound
            };
        }

        if (tournament.CurrentTeams >= tournament.MaxTeams)
        {
            return new CreateAdminTeamResult
            {
                Status = CreateAdminTeamStatus.TournamentFull
            };
        }

        if (command.PlayerNames.Count != tournament.PlayersPerTeam)
        {
            return new CreateAdminTeamResult
            {
                Status = CreateAdminTeamStatus.InvalidPlayerCount
            };
        }

        var createdTeam = await CreateTeamAsync(
            tournament,
            command.TeamName,
            command.CaptainId,
            command.PlayerNames,
            TeamStatus.Accepted);

        tournament.CurrentTeams += 1;
        tournament.TeamStatus = tournament.CurrentTeams >= tournament.MaxTeams
            ? TournamentTeamStatus.Full
            : TournamentTeamStatus.Open;

        await tournament.Update<TournamentInsert>();

        return new CreateAdminTeamResult
        {
            Status = CreateAdminTeamStatus.Success,
            Team = createdTeam
        };
    }

    public async Task<ReviewTeamRequestResult> ReviewAsync(ReviewTeamRequestCommand command)
    {
        var teamResponse = await _supabase
            .From<Team>()
            .Filter("id", PostgrestOperator.Equals, command.TeamId.ToString())
            .Limit(1)
            .Get();

        var team = teamResponse.Models.FirstOrDefault();
        if (team == null)
        {
            return new ReviewTeamRequestResult
            {
                Status = ReviewTeamRequestStatus.TeamNotFound
            };
        }

        var tournamentResponse = await _supabase
            .From<TournamentInsert>()
            .Filter("id", PostgrestOperator.Equals, team.TournamentId.ToString())
            .Limit(1)
            .Get();

        var tournament = tournamentResponse.Models.FirstOrDefault();
        if (tournament == null)
        {
            return new ReviewTeamRequestResult
            {
                Status = ReviewTeamRequestStatus.TournamentNotFound
            };
        }

        if (command.Status == TeamStatus.Rejected && string.IsNullOrWhiteSpace(command.RejectionReason))
        {
            return new ReviewTeamRequestResult
            {
                Status = ReviewTeamRequestStatus.RejectionReasonRequired
            };
        }

        var currentTeams = tournament.CurrentTeams;
        if (team.Status != TeamStatus.Accepted && command.Status == TeamStatus.Accepted)
        {
            if (currentTeams >= tournament.MaxTeams)
            {
                return new ReviewTeamRequestResult
                {
                    Status = ReviewTeamRequestStatus.TournamentFull
                };
            }

            currentTeams += 1;
        }
        else if (team.Status == TeamStatus.Accepted && command.Status != TeamStatus.Accepted)
        {
            currentTeams -= 1;
        }

        if (currentTeams < 0)
        {
            currentTeams = 0;
        }

        var tournamentTeamStatus = currentTeams >= tournament.MaxTeams
            ? TournamentTeamStatus.Full
            : TournamentTeamStatus.Open;

        team.Status = command.Status;
        team.RejectionReason = command.Status == TeamStatus.Rejected
            ? command.RejectionReason!.Trim()
            : null;

        await team.Update<Team>();

        tournament.CurrentTeams = currentTeams;
        tournament.TeamStatus = tournamentTeamStatus;

        await tournament.Update<TournamentInsert>();

        await NotifyCaptainReviewResultAsync(team, tournament);

        return new ReviewTeamRequestResult
        {
            Status = ReviewTeamRequestStatus.Success,
            Team = team,
            CurrentTeams = currentTeams,
            TournamentTeamStatus = tournamentTeamStatus
        };
    }

    private async Task<TournamentInsert?> GetTournamentAsync(long tournamentId)
    {
        var tournamentResponse = await _supabase
            .From<TournamentInsert>()
            .Filter("id", PostgrestOperator.Equals, tournamentId.ToString())
            .Limit(1)
            .Get();

        return tournamentResponse.Models.FirstOrDefault();
    }

    private async Task<Team> CreateTeamAsync(
        TournamentInsert tournament,
        string teamName,
        Guid captainId,
        IReadOnlyCollection<string> playerNames,
        TeamStatus status)
    {
        var createdAt = DateTime.UtcNow;
        var teamToInsert = new Team
        {
            Name = teamName.Trim(),
            TournamentId = tournament.Id,
            CaptainId = captainId,
            Status = status,
            RejectionReason = null,
            CreatedAt = createdAt
        };

        var teamInsertResponse = await _supabase
            .From<Team>()
            .Insert(teamToInsert, new PostgrestQueryOptions());

        var createdTeam = teamInsertResponse.Models.FirstOrDefault() ?? teamToInsert;
        if (createdTeam.Id == 0)
        {
            var createdTeamResponse = await _supabase
                .From<Team>()
                .Filter("tournament_id", PostgrestOperator.Equals, tournament.Id.ToString())
                .Filter("captain_id", PostgrestOperator.Equals, captainId.ToString())
                .Filter("name", PostgrestOperator.Equals, teamToInsert.Name)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(1)
                .Get();

            createdTeam = createdTeamResponse.Models.FirstOrDefault() ?? teamToInsert;
        }

        if (createdTeam.Id == 0)
        {
            throw new InvalidOperationException("Unable to retrieve the created team after insert.");
        }

        var membersToInsert = playerNames
            .Select(playerName => new TeamMember
            {
                Name = playerName.Trim(),
                TeamId = createdTeam.Id,
                CreatedAt = createdAt
            })
            .ToList();

        await _supabase
            .From<TeamMember>()
            .Insert(membersToInsert, new PostgrestQueryOptions());

        if (status == TeamStatus.Pending)
        {
            await NotifyJoinRequestOrganizerAsync(tournament, createdTeam, captainId);
        }

        return createdTeam;
    }

    private async Task NotifyJoinRequestOrganizerAsync(
        TournamentInsert tournament,
        Team team,
        Guid captainId)
    {
        var organizerProfile = await GetProfileAsync(tournament.UserId);
        if (string.IsNullOrWhiteSpace(organizerProfile?.DiscordId))
        {
            return;
        }

        var captainProfile = await GetProfileAsync(captainId.ToString());
        var captainName =
            captainProfile?.Username ??
            captainProfile?.Name ??
            captainProfile?.Email ??
            team.Name;

        var sent = await _discordService.SendJoinRequestNotificationAsync(
            organizerProfile.DiscordId,
            team.Name,
            captainName,
            BuildPublicUrl($"/tournament/{tournament.Slug}/admin/teams"));

        if (!sent)
        {
            _logger.LogInformation(
                "Join request organizer DM could not be delivered for tournament {TournamentId}.",
                tournament.Id);
        }
    }

    private async Task NotifyCaptainReviewResultAsync(Team team, TournamentInsert tournament)
    {
        var captainProfile = await GetProfileAsync(team.CaptainId.ToString());
        if (string.IsNullOrWhiteSpace(captainProfile?.DiscordId))
        {
            return;
        }

        var sent = await _discordService.SendJoinRequestResponseAsync(
            captainProfile.DiscordId,
            team.Status == TeamStatus.Accepted,
            tournament.Name,
            tournament.StartDate,
            team.RejectionReason);

        if (!sent)
        {
            _logger.LogInformation(
                "Join request response DM could not be delivered for team {TeamId}.",
                team.Id);
        }
    }

    private async Task<Profile?> GetProfileAsync(string userId)
    {
        var response = await _supabase
            .From<Profile>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, userId)
            .Limit(1)
            .Get();

        return response.Models.FirstOrDefault();
    }

    private string BuildPublicUrl(string relativePath)
    {
        var baseUrl = _configuration["App:PublicBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? relativePath
            : $"{baseUrl}{relativePath}";
    }
}

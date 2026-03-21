using api.Models;
using PostgrestOperator = Supabase.Postgrest.Constants.Operator;
using PostgrestQueryOptions = Supabase.Postgrest.QueryOptions;
using SupabaseClient = Supabase.Client;

namespace api.Services;

public enum PlaceTournamentBetStatus
{
    Success,
    TournamentNotFound,
    BettingClosed,
    InvalidTeam,
    BetAlreadyExists,
    InvalidCoinsBet,
    ProfileNotFound,
    InsufficientCoins
}

public sealed class PlaceTournamentBetCommand
{
    public Guid UserId { get; init; }
    public long TournamentId { get; init; }
    public long TeamId { get; init; }
    public int CoinsBet { get; init; }
}

public sealed class PlaceTournamentBetResult
{
    public PlaceTournamentBetStatus Status { get; init; }
    public long? BetId { get; init; }
    public int? RemainingCoins { get; init; }
}

public enum ResolveTournamentBetsStatus
{
    Success,
    TournamentNotFound,
    TournamentNotFinished,
    WinnerNotDefined
}

public sealed class ResolveTournamentBetsResult
{
    public ResolveTournamentBetsStatus Status { get; init; }
    public int BetsWon { get; init; }
    public int BetsLost { get; init; }
    public int PayoutCount { get; init; }
}

public sealed class TournamentBetState
{
    public bool BettingOpen { get; init; }
    public int CoinsBalance { get; init; }
    public TournamentBetSummary? Bet { get; init; }
}

public sealed class TournamentBetSummary
{
    public long Id { get; init; }
    public long TournamentId { get; init; }
    public long TeamId { get; init; }
    public int CoinsBet { get; init; }
    public string Status { get; init; } = "pending";
    public bool PaidOut { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PaidOutAt { get; init; }
}

public class BettingService
{
    private readonly SupabaseClient _supabase;
    private readonly IDiscordService _discordService;
    private readonly ILogger<BettingService> _logger;

    public BettingService(
        SupabaseClient supabase,
        IDiscordService discordService,
        ILogger<BettingService> logger)
    {
        _supabase = supabase;
        _discordService = discordService;
        _logger = logger;
    }

    public async Task<PlaceTournamentBetResult> PlaceTournamentBetAsync(PlaceTournamentBetCommand command)
    {
        if (command.CoinsBet <= 0)
        {
            return new PlaceTournamentBetResult
            {
                Status = PlaceTournamentBetStatus.InvalidCoinsBet
            };
        }

        var tournament = await GetTournamentAsync(command.TournamentId);
        if (tournament == null)
        {
            return new PlaceTournamentBetResult
            {
                Status = PlaceTournamentBetStatus.TournamentNotFound
            };
        }

        if (!string.Equals(tournament.Status, "aankomend", StringComparison.OrdinalIgnoreCase))
        {
            return new PlaceTournamentBetResult
            {
                Status = PlaceTournamentBetStatus.BettingClosed
            };
        }

        var team = await GetAcceptedTeamAsync(command.TeamId, command.TournamentId);
        if (team == null)
        {
            return new PlaceTournamentBetResult
            {
                Status = PlaceTournamentBetStatus.InvalidTeam
            };
        }

        var existingBet = await GetBetAsync(command.UserId, command.TournamentId);
        if (existingBet != null)
        {
            return new PlaceTournamentBetResult
            {
                Status = PlaceTournamentBetStatus.BetAlreadyExists
            };
        }

        var profile = await GetProfileAsync(command.UserId);
        if (profile == null)
        {
            return new PlaceTournamentBetResult
            {
                Status = PlaceTournamentBetStatus.ProfileNotFound
            };
        }

        if (profile.Coins < command.CoinsBet)
        {
            return new PlaceTournamentBetResult
            {
                Status = PlaceTournamentBetStatus.InsufficientCoins
            };
        }

        var previousCoins = profile.Coins;
        profile.Coins -= command.CoinsBet;
        await profile.Update<Profile>();

        try
        {
            var betToInsert = new Bet
            {
                UserId = command.UserId,
                TournamentId = command.TournamentId,
                TeamId = command.TeamId,
                CoinsBet = command.CoinsBet,
                Status = "pending",
                PaidOut = false
            };

            await _supabase.From<Bet>().Insert(betToInsert, new PostgrestQueryOptions());

            var createdBet = await GetBetAsync(command.UserId, command.TournamentId);

            return new PlaceTournamentBetResult
            {
                Status = PlaceTournamentBetStatus.Success,
                BetId = createdBet?.Id,
                RemainingCoins = profile.Coins
            };
        }
        catch (Exception)
        {
            profile.Coins = previousCoins;
            await profile.Update<Profile>();

            var refreshedBet = await GetBetAsync(command.UserId, command.TournamentId);
            if (refreshedBet != null)
            {
                return new PlaceTournamentBetResult
                {
                    Status = PlaceTournamentBetStatus.BetAlreadyExists,
                    BetId = refreshedBet.Id,
                    RemainingCoins = previousCoins
                };
            }

            throw;
        }
    }

    public async Task<TournamentBetState?> GetTournamentBetStateAsync(Guid userId, long tournamentId)
    {
        var profile = await GetProfileAsync(userId);
        var tournament = await GetTournamentAsync(tournamentId);
        if (profile == null || tournament == null)
        {
            return null;
        }

        var bet = await GetBetAsync(userId, tournamentId);
        return new TournamentBetState
        {
            BettingOpen = string.Equals(tournament.Status, "aankomend", StringComparison.OrdinalIgnoreCase) && bet == null,
            CoinsBalance = profile.Coins,
            Bet = bet == null
                ? null
                : new TournamentBetSummary
                {
                    Id = bet.Id,
                    TournamentId = bet.TournamentId,
                    TeamId = bet.TeamId,
                    CoinsBet = bet.CoinsBet,
                    Status = bet.Status,
                    PaidOut = bet.PaidOut,
                    CreatedAt = bet.CreatedAt,
                    PaidOutAt = bet.PaidOutAt
                }
        };
    }

    public async Task<bool> IsTournamentOrganizerAsync(long tournamentId, string organizerUserId)
    {
        var tournament = await GetTournamentAsync(tournamentId);
        return tournament != null
            && string.Equals(tournament.UserId, organizerUserId, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ResolveTournamentBetsResult> ResolveTournamentBetsAsync(long tournamentId)
    {
        var tournament = await GetTournamentAsync(tournamentId);
        if (tournament == null)
        {
            return new ResolveTournamentBetsResult
            {
                Status = ResolveTournamentBetsStatus.TournamentNotFound
            };
        }

        if (!string.Equals(tournament.Status, "finished", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolveTournamentBetsResult
            {
                Status = ResolveTournamentBetsStatus.TournamentNotFinished
            };
        }

        if (!tournament.WinnerTeamId.HasValue)
        {
            return new ResolveTournamentBetsResult
            {
                Status = ResolveTournamentBetsStatus.WinnerNotDefined
            };
        }

        var response = await _supabase
            .From<Bet>()
            .Select("*")
            .Filter("tournament_id", PostgrestOperator.Equals, tournamentId.ToString())
            .Get();

        var bets = response.Models;
        var now = DateTime.UtcNow;
        var wonCount = 0;
        var lostCount = 0;
        var payoutCount = 0;
        var winnerTeam = await GetTeamByIdAsync(tournament.WinnerTeamId.Value);
        var matchResult = $"Winnaar van {tournament.Name}: {winnerTeam?.Name ?? "Onbekend team"}";

        foreach (var bet in bets)
        {
            var nextStatus = bet.TeamId == tournament.WinnerTeamId.Value ? "won" : "lost";
            var statusChanged = !string.Equals(bet.Status, nextStatus, StringComparison.OrdinalIgnoreCase);
            if (nextStatus == "won")
            {
                wonCount += 1;
            }
            else
            {
                lostCount += 1;
            }

            if (statusChanged)
            {
                bet.Status = nextStatus;
                await bet.Update<Bet>();
            }

            var profile = await GetProfileAsync(bet.UserId);
            if (profile == null)
            {
                continue;
            }

            if (nextStatus == "lost" && statusChanged)
            {
                await NotifyBetResultAsync(profile, won: false, matchResult, bet.CoinsBet);
            }

            if (nextStatus != "won" || bet.PaidOut)
            {
                continue;
            }

            profile.Coins += bet.CoinsBet * 2;
            profile.LifetimeCoins = (profile.LifetimeCoins ?? 0) + (bet.CoinsBet * 2);
            await profile.Update<Profile>();

            bet.PaidOut = true;
            bet.PaidOutAt = now;
            await bet.Update<Bet>();
            payoutCount += 1;

            await NotifyBetResultAsync(profile, won: true, matchResult, bet.CoinsBet * 2);
        }

        return new ResolveTournamentBetsResult
        {
            Status = ResolveTournamentBetsStatus.Success,
            BetsWon = wonCount,
            BetsLost = lostCount,
            PayoutCount = payoutCount
        };
    }

    public async Task RefundAndDeleteTournamentBetsAsync(
        long tournamentId,
        CancellationToken cancellationToken = default)
    {
        var response = await _supabase
            .From<Bet>()
            .Select("*")
            .Filter("tournament_id", PostgrestOperator.Equals, tournamentId.ToString())
            .Get(cancellationToken);

        foreach (var bet in response.Models)
        {
            if (!string.Equals(bet.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var profile = await GetProfileAsync(bet.UserId, cancellationToken);
            if (profile == null)
            {
                continue;
            }

            profile.Coins += bet.CoinsBet;
            await profile.Update<Profile>(cancellationToken: cancellationToken);
        }

        await _supabase
            .From<Bet>()
            .Filter("tournament_id", PostgrestOperator.Equals, tournamentId.ToString())
            .Delete(cancellationToken: cancellationToken);
    }

    private async Task<TournamentInsert?> GetTournamentAsync(
        long tournamentId,
        CancellationToken cancellationToken = default)
    {
        var response = await _supabase
            .From<TournamentInsert>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, tournamentId.ToString())
            .Limit(1)
            .Get(cancellationToken);

        return response.Models.FirstOrDefault();
    }

    private async Task<Team?> GetAcceptedTeamAsync(long teamId, long tournamentId)
    {
        var response = await _supabase
            .From<Team>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, teamId.ToString())
            .Filter("tournament_id", PostgrestOperator.Equals, tournamentId.ToString())
            .Filter("status", PostgrestOperator.Equals, "accepted")
            .Limit(1)
            .Get();

        return response.Models.FirstOrDefault();
    }

    private async Task<Profile?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _supabase
            .From<Profile>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, userId.ToString())
            .Limit(1)
            .Get(cancellationToken);

        return response.Models.FirstOrDefault();
    }

    private async Task<Bet?> GetBetAsync(Guid userId, long tournamentId)
    {
        var response = await _supabase
            .From<Bet>()
            .Select("*")
            .Filter("user_id", PostgrestOperator.Equals, userId.ToString())
            .Filter("tournament_id", PostgrestOperator.Equals, tournamentId.ToString())
            .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
            .Limit(1)
            .Get();

        return response.Models.FirstOrDefault();
    }

    private async Task<Team?> GetTeamByIdAsync(long teamId)
    {
        var response = await _supabase
            .From<Team>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, teamId.ToString())
            .Limit(1)
            .Get();

        return response.Models.FirstOrDefault();
    }

    private async Task NotifyBetResultAsync(
        Profile profile,
        bool won,
        string matchResult,
        int coinsAmount)
    {
        if (string.IsNullOrWhiteSpace(profile.DiscordId))
        {
            return;
        }

        var sent = await _discordService.SendBetResultAsync(
            profile.DiscordId,
            won,
            matchResult,
            coinsAmount);

        if (!sent)
        {
            _logger.LogInformation(
                "Bet result DM could not be delivered to Discord user {DiscordId}.",
                profile.DiscordId);
        }
    }
}

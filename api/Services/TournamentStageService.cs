using api.Models;
using PostgrestOperator = Supabase.Postgrest.Constants.Operator;
using SupabaseClient = Supabase.Client;

namespace api.Services;

public enum CreateBracketStageStatus
{
    Success,
    TournamentNotFound,
    OrganizerOnly,
    TournamentAlreadyLaunched,
    NotEnoughTeams,
    StructureAlreadyExists,
    InvalidManualSeeding
}

public enum UpdateMatchResultStatus
{
    Success,
    TournamentNotFound,
    OrganizerOnly,
    TournamentNotEditable,
    MatchNotFound,
    MatchNotReady,
    InvalidScore
}

public sealed class BracketSlotAssignment
{
    public int SlotIndex { get; init; }
    public long? TeamId { get; init; }
}

public sealed class CreateBracketStageCommand
{
    public string Identifier { get; init; } = string.Empty;
    public string OrganizerUserId { get; init; } = string.Empty;
    public string Mode { get; init; } = "automatic";
    public IReadOnlyCollection<BracketSlotAssignment> Slots { get; init; } = Array.Empty<BracketSlotAssignment>();
}

public sealed class CreateBracketStageResult
{
    public CreateBracketStageStatus Status { get; init; }
    public TournamentInsert? Tournament { get; init; }
    public IReadOnlyCollection<Team> AcceptedTeams { get; init; } = Array.Empty<Team>();
    public IReadOnlyCollection<Match> Matches { get; init; } = Array.Empty<Match>();
}

public sealed class TournamentStageOverview
{
    public TournamentInsert Tournament { get; init; } = null!;
    public IReadOnlyCollection<Team> AcceptedTeams { get; init; } = Array.Empty<Team>();
    public IReadOnlyCollection<Match> Matches { get; init; } = Array.Empty<Match>();
}

public sealed class UpdateMatchResultCommand
{
    public string Identifier { get; init; } = string.Empty;
    public string OrganizerUserId { get; init; } = string.Empty;
    public long MatchId { get; init; }
    public long Team1Score { get; init; }
    public long Team2Score { get; init; }
}

public sealed class UpdateMatchResultResult
{
    public UpdateMatchResultStatus Status { get; init; }
    public Match? Match { get; init; }
    public long TournamentId { get; init; }
}

public class TournamentStageService
{
    private readonly SupabaseClient _supabase;

    public TournamentStageService(SupabaseClient supabase)
    {
        _supabase = supabase;
    }

    public async Task<TournamentStageOverview?> GetOverviewAsync(string identifier)
    {
        var tournament = await GetTournamentAsync(identifier);
        if (tournament == null)
        {
            return null;
        }

        var acceptedTeams = await GetAcceptedTeamsAsync(tournament.Id);
        var matches = await GetMatchesAsync(tournament.Id);

        return new TournamentStageOverview
        {
            Tournament = tournament,
            AcceptedTeams = acceptedTeams,
            Matches = matches
        };
    }

    public async Task<CreateBracketStageResult> CreateBracketAsync(CreateBracketStageCommand command)
    {
        var tournament = await GetTournamentAsync(command.Identifier);
        if (tournament == null)
        {
            return new CreateBracketStageResult
            {
                Status = CreateBracketStageStatus.TournamentNotFound
            };
        }

        if (!string.Equals(tournament.UserId, command.OrganizerUserId, StringComparison.OrdinalIgnoreCase))
        {
            return new CreateBracketStageResult
            {
                Status = CreateBracketStageStatus.OrganizerOnly
            };
        }

        var acceptedTeams = await GetAcceptedTeamsAsync(tournament.Id);
        if (tournament.TournamentType != "pending")
        {
            return new CreateBracketStageResult
            {
                Status = CreateBracketStageStatus.TournamentAlreadyLaunched,
                Tournament = tournament,
                AcceptedTeams = acceptedTeams
            };
        }

        if (acceptedTeams.Count < tournament.MinTeams)
        {
            return new CreateBracketStageResult
            {
                Status = CreateBracketStageStatus.NotEnoughTeams,
                Tournament = tournament,
                AcceptedTeams = acceptedTeams
            };
        }

        var existingMatches = await GetMatchesAsync(tournament.Id);
        if (existingMatches.Count > 0)
        {
            return new CreateBracketStageResult
            {
                Status = CreateBracketStageStatus.StructureAlreadyExists,
                Tournament = tournament,
                AcceptedTeams = acceptedTeams,
                Matches = existingMatches
            };
        }

        var seededSlots = command.Mode == "manual"
            ? BuildManualSlots(command.Slots, acceptedTeams)
            : BuildAutomaticSlots(acceptedTeams);

        if (seededSlots == null)
        {
            return new CreateBracketStageResult
            {
                Status = CreateBracketStageStatus.InvalidManualSeeding,
                Tournament = tournament,
                AcceptedTeams = acceptedTeams
            };
        }

        var matches = BuildBracketMatches(tournament.Id, seededSlots);
        await _supabase.From<Match>().Insert(matches);

        tournament.TournamentType = "bracket";
        tournament.Status = "live";
        await tournament.Update<TournamentInsert>();

        return new CreateBracketStageResult
        {
            Status = CreateBracketStageStatus.Success,
            Tournament = tournament,
            AcceptedTeams = acceptedTeams,
            Matches = matches
        };
    }

    public async Task<UpdateMatchResultResult> UpdateMatchResultAsync(UpdateMatchResultCommand command)
    {
        var tournament = await GetTournamentAsync(command.Identifier);
        if (tournament == null)
        {
            return new UpdateMatchResultResult
            {
                Status = UpdateMatchResultStatus.TournamentNotFound
            };
        }

        if (!string.Equals(tournament.UserId, command.OrganizerUserId, StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateMatchResultResult
            {
                Status = UpdateMatchResultStatus.OrganizerOnly
            };
        }

        var tournamentStatus = GetEffectiveTournamentStatus(tournament);
        if (!string.Equals(tournamentStatus, "live", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(tournamentStatus, "finished", StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateMatchResultResult
            {
                Status = UpdateMatchResultStatus.TournamentNotEditable
            };
        }

        var matchResponse = await _supabase
            .From<Match>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, command.MatchId.ToString())
            .Filter("tournament_id", PostgrestOperator.Equals, tournament.Id.ToString())
            .Limit(1)
            .Get();

        var match = matchResponse.Models.FirstOrDefault();
        if (match == null)
        {
            return new UpdateMatchResultResult
            {
                Status = UpdateMatchResultStatus.MatchNotFound
            };
        }

        if (!match.Team1Id.HasValue || !match.Team2Id.HasValue)
        {
            return new UpdateMatchResultResult
            {
                Status = UpdateMatchResultStatus.MatchNotReady
            };
        }

        var winsNeeded = await GetWinsNeededAsync(tournament, match);
        if (!IsValidScore(command.Team1Score, command.Team2Score, winsNeeded))
        {
            return new UpdateMatchResultResult
            {
                Status = UpdateMatchResultStatus.InvalidScore
            };
        }

        var previousWinnerId = match.WinnerId;
        var nextWinnerId = ResolveWinnerId(match, command.Team1Score, command.Team2Score, winsNeeded);

        match.Team1Score = command.Team1Score;
        match.Team2Score = command.Team2Score;
        match.WinnerId = nextWinnerId;
        await match.Update<Match>();

        if (previousWinnerId != nextWinnerId)
        {
            await PropagateWinnerToNextMatchAsync(tournament, match, nextWinnerId);
        }

        return new UpdateMatchResultResult
        {
            Status = UpdateMatchResultStatus.Success,
            Match = match,
            TournamentId = tournament.Id
        };
    }

    private async Task<TournamentInsert?> GetTournamentAsync(string identifier)
    {
        var query = _supabase.From<TournamentInsert>().Select("*").Limit(1);
        var response = long.TryParse(identifier, out var tournamentId)
            ? await query.Filter("id", PostgrestOperator.Equals, tournamentId.ToString()).Get()
            : await query.Filter("slug", PostgrestOperator.Equals, identifier).Get();

        return response.Models.FirstOrDefault();
    }

    private async Task<List<Team>> GetAcceptedTeamsAsync(long tournamentId)
    {
        var response = await _supabase
            .From<Team>()
            .Select("*")
            .Filter("tournament_id", PostgrestOperator.Equals, tournamentId.ToString())
            .Filter("status", PostgrestOperator.Equals, "accepted")
            .Order("created_at", Supabase.Postgrest.Constants.Ordering.Ascending)
            .Get();

        return response.Models;
    }

    private async Task<List<Match>> GetMatchesAsync(long tournamentId)
    {
        var response = await _supabase
            .From<Match>()
            .Select("*")
            .Filter("tournament_id", PostgrestOperator.Equals, tournamentId.ToString())
            .Order("round", Supabase.Postgrest.Constants.Ordering.Ascending)
            .Order("match_number", Supabase.Postgrest.Constants.Ordering.Ascending)
            .Get();

        return response.Models;
    }

    private async Task<int> GetWinsNeededAsync(TournamentInsert tournament, Match match)
    {
        var formatId = tournament.Format;
        var maxRound = await GetMaxRoundAsync(tournament.Id);

        if (match.Round > 0 && tournament.FinalFormat.HasValue && match.Round == maxRound)
        {
            formatId = tournament.FinalFormat.Value;
        }

        var response = await _supabase
            .From<Format>()
            .Select("name")
            .Filter("id", PostgrestOperator.Equals, formatId.ToString())
            .Limit(1)
            .Get();

        var formatName = response.Models.FirstOrDefault()?.Name ?? "BO1";
        var digits = new string(formatName.Where(char.IsDigit).ToArray());
        var bestOf = int.TryParse(digits, out var parsedValue) && parsedValue > 0
            ? parsedValue
            : 1;

        return (int)Math.Ceiling(bestOf / 2d);
    }

    private async Task<long> GetMaxRoundAsync(long tournamentId)
    {
        var response = await _supabase
            .From<Match>()
            .Select("round")
            .Filter("tournament_id", PostgrestOperator.Equals, tournamentId.ToString())
            .Order("round", Supabase.Postgrest.Constants.Ordering.Descending)
            .Limit(1)
            .Get();

        return response.Models.FirstOrDefault()?.Round ?? 1;
    }

    private static string GetEffectiveTournamentStatus(TournamentInsert tournament)
    {
        if (string.Equals(tournament.Status, "finished", StringComparison.OrdinalIgnoreCase))
        {
            return "finished";
        }

        var now = DateTime.UtcNow;
        if (tournament.EndDate != default && now > tournament.EndDate.ToUniversalTime())
        {
            return "finished";
        }

        if (string.Equals(tournament.Status, "live", StringComparison.OrdinalIgnoreCase))
        {
            return "live";
        }

        if (tournament.StartDate != default && now >= tournament.StartDate.ToUniversalTime())
        {
            return "live";
        }

        return "aankomend";
    }

    private static List<long?>? BuildManualSlots(
        IReadOnlyCollection<BracketSlotAssignment> slots,
        IReadOnlyCollection<Team> acceptedTeams)
    {
        var bracketSize = GetBracketSize(acceptedTeams.Count);
        if (slots.Count != bracketSize)
        {
            return null;
        }

        var orderedSlots = slots
            .OrderBy(slot => slot.SlotIndex)
            .ToList();

        if (orderedSlots.Select((slot, index) => slot.SlotIndex != index).Any(invalid => invalid))
        {
            return null;
        }

        var acceptedTeamIds = acceptedTeams.Select(team => team.Id).ToHashSet();
        var assignedTeamIds = orderedSlots
            .Where(slot => slot.TeamId.HasValue)
            .Select(slot => slot.TeamId!.Value)
            .ToList();

        if (assignedTeamIds.Count != acceptedTeams.Count)
        {
            return null;
        }

        if (assignedTeamIds.Distinct().Count() != assignedTeamIds.Count)
        {
            return null;
        }

        if (assignedTeamIds.Any(teamId => !acceptedTeamIds.Contains(teamId)))
        {
            return null;
        }

        return orderedSlots
            .Select(slot => slot.TeamId)
            .ToList();
    }

    private static List<long?> BuildAutomaticSlots(IReadOnlyCollection<Team> acceptedTeams)
    {
        var bracketSize = GetBracketSize(acceptedTeams.Count);
        var slots = Enumerable.Repeat<long?>(null, bracketSize).ToList();
        var orderedTeams = acceptedTeams.OrderBy(team => team.CreatedAt).ToList();

        for (var index = 0; index < orderedTeams.Count; index += 1)
        {
            slots[index] = orderedTeams[index].Id;
        }

        return slots;
    }

    private static List<Match> BuildBracketMatches(long tournamentId, IReadOnlyList<long?> slots)
    {
        var bracketSize = slots.Count;
        var rounds = (int)Math.Log2(bracketSize);
        var matches = new List<Match>();

        for (var round = 1; round <= rounds; round += 1)
        {
            var matchesInRound = bracketSize / (int)Math.Pow(2, round);
            for (var matchNumber = 1; matchNumber <= matchesInRound; matchNumber += 1)
            {
                if (round == 1)
                {
                    var baseSlotIndex = (matchNumber - 1) * 2;
                    matches.Add(new Match
                    {
                        TournamentId = tournamentId,
                        Round = round,
                        MatchNumber = matchNumber,
                        Team1Id = slots[baseSlotIndex],
                        Team2Id = slots[baseSlotIndex + 1],
                        Team1Score = 0,
                        Team2Score = 0
                    });
                    continue;
                }

                matches.Add(new Match
                {
                    TournamentId = tournamentId,
                    Round = round,
                    MatchNumber = matchNumber,
                    Team1Score = 0,
                    Team2Score = 0
                });
            }
        }

        return matches;
    }

    private static int GetBracketSize(int teamCount)
    {
        var size = 2;
        while (size < teamCount)
        {
            size *= 2;
        }

        return size;
    }

    private static bool IsValidScore(long team1Score, long team2Score, int winsNeeded)
    {
        if (
            team1Score < 0 ||
            team2Score < 0 ||
            team1Score > winsNeeded ||
            team2Score > winsNeeded)
        {
            return false;
        }

        if (team1Score == winsNeeded && team2Score == winsNeeded)
        {
            return false;
        }

        return true;
    }

    private static long? ResolveWinnerId(Match match, long team1Score, long team2Score, int winsNeeded)
    {
        if (team1Score == winsNeeded && team2Score < winsNeeded)
        {
            return match.Team1Id;
        }

        if (team2Score == winsNeeded && team1Score < winsNeeded)
        {
            return match.Team2Id;
        }

        return null;
    }

    private async Task PropagateWinnerToNextMatchAsync(TournamentInsert tournament, Match match, long? winnerId)
    {
        if (match.Round <= 0)
        {
            return;
        }

        var nextRound = match.Round + 1;
        var nextMatchNumber = (long)Math.Ceiling(match.MatchNumber / 2d);

        var nextMatchResponse = await _supabase
            .From<Match>()
            .Select("*")
            .Filter("tournament_id", PostgrestOperator.Equals, tournament.Id.ToString())
            .Filter("round", PostgrestOperator.Equals, nextRound.ToString())
            .Filter("match_number", PostgrestOperator.Equals, nextMatchNumber.ToString())
            .Limit(1)
            .Get();

        var nextMatch = nextMatchResponse.Models.FirstOrDefault();
        if (nextMatch == null)
        {
            tournament.WinnerTeamId = winnerId;
            tournament.Status = winnerId.HasValue ? "finished" : "live";
            await tournament.Update<TournamentInsert>();
            return;
        }

        if (match.MatchNumber % 2 == 1)
        {
            nextMatch.Team1Id = winnerId;
        }
        else
        {
            nextMatch.Team2Id = winnerId;
        }

        nextMatch.Team1Score = 0;
        nextMatch.Team2Score = 0;
        nextMatch.WinnerId = null;
        await nextMatch.Update<Match>();

        await PropagateWinnerToNextMatchAsync(tournament, nextMatch, null);
    }
}

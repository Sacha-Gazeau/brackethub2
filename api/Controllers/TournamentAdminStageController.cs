using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/tournaments/{identifier}/admin/stage")]
public class TournamentAdminStageController : ControllerBase
{
    private static readonly HttpClient HttpClient = new();

    private readonly BettingService _bettingService;
    private readonly TournamentStageService _tournamentStageService;
    private readonly IConfiguration _configuration;

    public TournamentAdminStageController(
        BettingService bettingService,
        TournamentStageService tournamentStageService,
        IConfiguration configuration)
    {
        _bettingService = bettingService;
        _tournamentStageService = tournamentStageService;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetOverview(string identifier)
    {
        var overview = await _tournamentStageService.GetOverviewAsync(identifier);
        if (overview == null)
        {
            return NotFound(new { message = "Tournament not found." });
        }

        return Ok(new
        {
            tournament = new
            {
                id = overview.Tournament.Id,
                name = overview.Tournament.Name,
                slug = overview.Tournament.Slug,
                user_id = overview.Tournament.UserId,
                format = overview.Tournament.Format,
                final_format = overview.Tournament.FinalFormat,
                current_teams = overview.Tournament.CurrentTeams,
                min_teams = overview.Tournament.MinTeams,
                max_teams = overview.Tournament.MaxTeams,
                players_per_team = overview.Tournament.PlayersPerTeam,
                tournament_type = overview.Tournament.TournamentType
            },
            accepted_teams = overview.AcceptedTeams.Select(team => new
            {
                id = team.Id,
                name = team.Name,
                captain_id = team.CaptainId,
                status = team.Status,
                created_at = team.CreatedAt
            }),
            matches = overview.Matches.Select(match => new
            {
                id = match.Id,
                round = match.Round,
                match_number = match.MatchNumber,
                team1_id = match.Team1Id,
                team2_id = match.Team2Id,
                winner_id = match.WinnerId,
                team1_score = match.Team1Score,
                team2_score = match.Team2Score,
                scheduled_at = match.ScheduledAt
            })
        });
    }

    [HttpPost("bracket")]
    public async Task<IActionResult> CreateBracket(
        string identifier,
        [FromBody] CreateBracketStageBody request)
    {
        var (isAuthenticated, userId, authErrorMessage) = await TryGetAuthenticatedUserIdAsync();
        if (!isAuthenticated || string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = authErrorMessage });
        }

        var result = await _tournamentStageService.CreateBracketAsync(new CreateBracketStageCommand
        {
            Identifier = identifier,
            OrganizerUserId = userId,
            Mode = string.IsNullOrWhiteSpace(request.Mode) ? "automatic" : request.Mode,
            Slots = request.Slots
        });

        return result.Status switch
        {
            CreateBracketStageStatus.TournamentNotFound => NotFound(new { message = "Tournament not found." }),
            CreateBracketStageStatus.OrganizerOnly => StatusCode(403, new { message = "Only the organizer can manage this stage." }),
            CreateBracketStageStatus.TournamentAlreadyLaunched => BadRequest(new { message = "Tournament stage has already been launched." }),
            CreateBracketStageStatus.NotEnoughTeams => BadRequest(new { message = "Not enough accepted teams to launch the tournament." }),
            CreateBracketStageStatus.StructureAlreadyExists => BadRequest(new { message = "A stage structure already exists for this tournament." }),
            CreateBracketStageStatus.InvalidManualSeeding => BadRequest(new { message = "Manual bracket seeding is invalid." }),
            _ => Ok(new
            {
                message = "Bracket created successfully.",
                tournament_type = result.Tournament?.TournamentType,
                matches = result.Matches.Select(match => new
                {
                    id = match.Id,
                    round = match.Round,
                    match_number = match.MatchNumber,
                    team1_id = match.Team1Id,
                    team2_id = match.Team2Id
                })
            })
        };
    }

    [HttpPost("matches/{matchId:long}/result")]
    public async Task<IActionResult> SaveMatchResult(
        string identifier,
        long matchId,
        [FromBody] UpdateMatchResultBody request)
    {
        var (isAuthenticated, userId, authErrorMessage) = await TryGetAuthenticatedUserIdAsync();
        if (!isAuthenticated || string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = authErrorMessage });
        }

        var result = await _tournamentStageService.UpdateMatchResultAsync(new UpdateMatchResultCommand
        {
            Identifier = identifier,
            OrganizerUserId = userId,
            MatchId = matchId,
            Team1Score = request.Team1Score,
            Team2Score = request.Team2Score
        });

        if (result.Status == UpdateMatchResultStatus.Success && result.TournamentId > 0)
        {
            await _bettingService.ResolveTournamentBetsAsync(result.TournamentId);
        }

        return result.Status switch
        {
            UpdateMatchResultStatus.TournamentNotFound => NotFound(new { message = "Tournament not found." }),
            UpdateMatchResultStatus.OrganizerOnly => StatusCode(403, new { message = "Only the organizer can manage this stage." }),
            UpdateMatchResultStatus.MatchNotFound => NotFound(new { message = "Match not found." }),
            UpdateMatchResultStatus.MatchNotReady => BadRequest(new { message = "This match is not ready yet." }),
            UpdateMatchResultStatus.InvalidScore => BadRequest(new { message = "Invalid score for this best-of format." }),
            _ => Ok(new
            {
                message = "Match result saved successfully.",
                match = result.Match == null
                    ? null
                    : new
                    {
                        id = result.Match.Id,
                        round = result.Match.Round,
                        match_number = result.Match.MatchNumber,
                        team1_id = result.Match.Team1Id,
                        team2_id = result.Match.Team2Id,
                        winner_id = result.Match.WinnerId,
                        team1_score = result.Match.Team1Score,
                        team2_score = result.Match.Team2Score
                    }
            })
        };
    }

    private async Task<(bool IsAuthenticated, string? UserId, string ErrorMessage)> TryGetAuthenticatedUserIdAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return (false, null, "Missing or invalid Authorization header.");
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, null, "Missing bearer token.");
        }

        var supabaseUrl = _configuration["Supabase:Url"];
        if (string.IsNullOrWhiteSpace(supabaseUrl))
        {
            return (false, null, "Server is missing Supabase:Url configuration.");
        }

        var apiKey = _configuration["Supabase:PublishableKey"]
                     ?? _configuration["Supabase:AnonKey"]
                     ?? _configuration["Supabase:ServiceKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "Server is missing Supabase API key configuration.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{supabaseUrl.TrimEnd('/')}/auth/v1/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("apikey", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.SendAsync(request);
        }
        catch
        {
            return (false, null, "Unable to verify token with Supabase Auth.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return (false, null, "Invalid or expired token.");
        }

        var body = await response.Content.ReadAsStringAsync();
        try
        {
            using var json = JsonDocument.Parse(body);
            var userId = json.RootElement.GetProperty("id").GetString();
            return string.IsNullOrWhiteSpace(userId)
                ? (false, null, "Supabase Auth returned an empty user id.")
                : (true, userId, string.Empty);
        }
        catch
        {
            return (false, null, "Invalid response from Supabase Auth.");
        }
    }
}

public sealed class CreateBracketStageBody
{
    public string Mode { get; init; } = "automatic";
    public IReadOnlyCollection<BracketSlotAssignment> Slots { get; init; } = Array.Empty<BracketSlotAssignment>();
}

public sealed class UpdateMatchResultBody
{
    [JsonPropertyName("team1_score")]
    public long Team1Score { get; init; }

    [JsonPropertyName("team2_score")]
    public long Team2Score { get; init; }
}

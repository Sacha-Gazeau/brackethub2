using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using Supabase;
using api.Models;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/tournaments")]
public class TournamentsController : ControllerBase
{
    private static readonly int[] AllowedTeamCounts = [2, 4, 8, 16, 32, 64];

    private readonly Client _supabase;
    private readonly ISupabaseAuthService _supabaseAuthService;

    public TournamentsController(Client supabase, ISupabaseAuthService supabaseAuthService)
    {
        _supabase = supabase;
        _supabaseAuthService = supabaseAuthService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTournament([FromBody] CreateTournamentRequest request)
    {
        try
        {
            var validationErrors = ValidateCreateTournamentRequest(request);
            if (validationErrors.Count > 0)
            {
                return BadRequest(new
                {
                    message = "Please fix the highlighted fields.",
                    errors = validationErrors
                });
            }

            var privacy = request.Privacy.Trim().ToLowerInvariant();
            DateTimeOffset.TryParse(request.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startDate);
            DateTimeOffset.TryParse(request.EndDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endDate);

            var (isAuthenticated, userId, authErrorMessage) = await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request);
            if (!isAuthenticated)
            {
                return Unauthorized(new { message = authErrorMessage });
            }

            var baseSlug = GenerateSlug(request.Name);
            if (string.IsNullOrWhiteSpace(baseSlug))
            {
                baseSlug = "tournament";
            }

            var uniqueSlug = await GenerateUniqueSlugAsync(baseSlug);

            var tournament = new TournamentInsert
            {
                Name = request.Name,
                CreatedAt = DateTime.UtcNow,
                Slug = uniqueSlug,
                GameIgdbId = request.GameIgdbId,
                Format = request.Format,
                MaxTeams = request.MaxTeams,
                MinTeams = request.MinTeams,
                CurrentTeams = 0,
                PlayersPerTeam = request.PlayersPerTeam,
                StartDate = startDate.UtcDateTime,
                EndDate = endDate.UtcDateTime,
                UserId = userId!,
                Status = "aankomend",
                FinalFormat = request.FinalFormat,
                Description = request.Description,
                Privacy = privacy
            };

            await _supabase.From<TournamentInsert>().Insert(tournament);

            return Ok(new
            {
                message = "Tournament created successfully.",
                slug = tournament.Slug,
                name = tournament.Name
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Unable to create the tournament right now. Please try again.",
                error = ex.Message
            });
        }
    }

    [HttpGet("{identifier}")]
    public async Task<IActionResult> GetTournament(string identifier)
    {
        try
        {
            var query = _supabase.From<TournamentInsert>().Select("*").Limit(1);
            var response = long.TryParse(identifier, out var tournamentId)
                ? await query.Filter("id", Supabase.Postgrest.Constants.Operator.Equals, tournamentId.ToString()).Get()
                : await query.Filter("slug", Supabase.Postgrest.Constants.Operator.Equals, identifier).Get();

            var tournament = response.Models.FirstOrDefault();
            if (tournament == null)
            {
                return NotFound(new { message = "Tournament not found." });
            }

            return Ok(MapTournamentResponse(tournament));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Unable to load the tournament right now.",
                error = ex.Message
            });
        }
    }

    private static Dictionary<string, string> ValidateCreateTournamentRequest(CreateTournamentRequest request)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = "Name is required.";
        }

        if (request.GameIgdbId <= 0)
        {
            errors["game_igdb_id"] = "Game is required.";
        }

        if (request.Format <= 0)
        {
            errors["format"] = "Format is required.";
        }

        if (!AllowedTeamCounts.Contains(request.MaxTeams))
        {
            errors["max_teams"] = "Team count must be one of: 2, 4, 8, 16, 32, 64.";
        }

        if (!AllowedTeamCounts.Contains(request.MinTeams))
        {
            errors["min_teams"] = "Minimum teams must be one of: 2, 4, 8, 16, 32, 64.";
        }
        else if (request.MinTeams > request.MaxTeams)
        {
            errors["min_teams"] = "Minimum teams cannot be greater than team count.";
        }

        if (request.PlayersPerTeam < 1)
        {
            errors["players_per_team"] = "Players per team must be at least 1.";
        }

        if (string.IsNullOrWhiteSpace(request.StartDate))
        {
            errors["start_date"] = "Start date is required.";
        }
        else if (!DateTimeOffset.TryParse(request.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            errors["start_date"] = "Start date format is invalid.";
        }

        if (string.IsNullOrWhiteSpace(request.EndDate))
        {
            errors["end_date"] = "End date is required.";
        }
        else if (!DateTimeOffset.TryParse(request.EndDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            errors["end_date"] = "End date format is invalid.";
        }

        if (string.IsNullOrWhiteSpace(request.Privacy))
        {
            errors["privacy"] = "Privacy is required.";
        }
        else
        {
            var privacy = request.Privacy.Trim().ToLowerInvariant();
            if (privacy != "public" && privacy != "friends" && privacy != "official")
            {
                errors["privacy"] = "Privacy must be 'public', 'friends' or 'official'.";
            }
        }

        return errors;
    }

    private async Task<string> GenerateUniqueSlugAsync(string baseSlug)
    {
        for (var suffix = 1; suffix < 10000; suffix++)
        {
            var candidateSlug = suffix == 1 ? baseSlug : $"{baseSlug}-{suffix}";
            var slugExists = await SlugExistsAsync(candidateSlug);
            if (!slugExists)
            {
                return candidateSlug;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique slug.");
    }

    private async Task<bool> SlugExistsAsync(string slug)
    {
        var response = await _supabase
            .From<TournamentInsert>()
            .Select("id")
            .Filter("slug", Supabase.Postgrest.Constants.Operator.Equals, slug)
            .Limit(1)
            .Get();

        return response.Models.Count > 0;
    }

    private static object MapTournamentResponse(TournamentInsert tournament)
    {
        return new
        {
            id = tournament.Id,
            created_at = tournament.CreatedAt,
            name = tournament.Name,
            slug = tournament.Slug,
            game_igdb_id = tournament.GameIgdbId,
            format = tournament.Format,
            max_teams = tournament.MaxTeams,
            min_teams = tournament.MinTeams,
            current_teams = tournament.CurrentTeams,
            players_per_team = tournament.PlayersPerTeam,
            start_date = tournament.StartDate,
            end_date = tournament.EndDate,
            user_id = tournament.UserId,
            status = tournament.Status,
            privacy = tournament.Privacy,
            team_status = tournament.TeamStatus,
            final_format = tournament.FinalFormat,
            description = tournament.Description,
            tournament_type = tournament.TournamentType,
            winner_team_id = tournament.WinnerTeamId
        };
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = slug.Normalize(NormalizationForm.FormD);
        var chars = slug.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        slug = new string(chars.ToArray()).Normalize(NormalizationForm.FormC);

        slug = slug
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("&", "and");

        var clean = new StringBuilder();
        var lastWasDash = false;
        foreach (var c in slug)
        {
            if (char.IsLetterOrDigit(c))
            {
                clean.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                clean.Append('-');
                lastWasDash = true;
            }
        }

        return clean.ToString().Trim('-');
    }
}

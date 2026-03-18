using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/tournaments")]
public class TournamentBetsController : ControllerBase
{
    private readonly BettingService _bettingService;
    private readonly ISupabaseAuthService _supabaseAuthService;

    public TournamentBetsController(
        BettingService bettingService,
        ISupabaseAuthService supabaseAuthService)
    {
        _bettingService = bettingService;
        _supabaseAuthService = supabaseAuthService;
    }

    [HttpGet("{identifier}/bets/me")]
    public async Task<IActionResult> GetMyBetState(string identifier)
    {
        try
        {
            var (isAuthenticated, userId, authErrorMessage) = await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request);
            if (!isAuthenticated || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized(new { message = authErrorMessage });
            }

            if (!long.TryParse(identifier, out var tournamentId))
            {
                return BadRequest(new { message = "Tournament id is invalid." });
            }

            var result = await _bettingService.GetTournamentBetStateAsync(parsedUserId, tournamentId);
            if (result == null)
            {
                return NotFound(new { message = "Tournament or profile not found." });
            }

            return Ok(new
            {
                betting_open = result.BettingOpen,
                coins_balance = result.CoinsBalance,
                bet = result.Bet == null
                    ? null
                    : new
                    {
                        id = result.Bet.Id,
                        tournament_id = result.Bet.TournamentId,
                        team_id = result.Bet.TeamId,
                        coins_bet = result.Bet.CoinsBet,
                        status = result.Bet.Status,
                        paid_out = result.Bet.PaidOut,
                        created_at = result.Bet.CreatedAt,
                        paid_out_at = result.Bet.PaidOutAt
                    }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Unable to load tournament bet state right now.",
                error = ex.Message
            });
        }
    }

    [HttpPost("bets")]
    public async Task<IActionResult> PlaceTournamentBet([FromBody] PlaceTournamentBetBody request)
    {
        try
        {
            var (isAuthenticated, userId, authErrorMessage) = await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request);
            if (!isAuthenticated || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized(new { message = authErrorMessage });
            }

            var result = await _bettingService.PlaceTournamentBetAsync(new PlaceTournamentBetCommand
            {
                UserId = parsedUserId,
                TournamentId = request.TournamentId,
                TeamId = request.TeamId,
                CoinsBet = request.CoinsBet
            });

            return result.Status switch
            {
                PlaceTournamentBetStatus.Success => Ok(new
                {
                    message = "Bet placed successfully.",
                    bet_id = result.BetId,
                    remaining_coins = result.RemainingCoins
                }),
                PlaceTournamentBetStatus.TournamentNotFound => NotFound(new { message = "Tournament not found." }),
                PlaceTournamentBetStatus.BettingClosed => BadRequest(new { message = "Betting is only available for upcoming tournaments." }),
                PlaceTournamentBetStatus.InvalidTeam => BadRequest(new { message = "Selected team is not a valid participant for this tournament." }),
                PlaceTournamentBetStatus.BetAlreadyExists => Conflict(new { message = "A bet already exists for this user on this tournament." }),
                PlaceTournamentBetStatus.InvalidCoinsBet => BadRequest(new { message = "Bet amount must be greater than zero." }),
                PlaceTournamentBetStatus.ProfileNotFound => NotFound(new { message = "Profile not found." }),
                PlaceTournamentBetStatus.InsufficientCoins => BadRequest(new { message = "Insufficient coins." }),
                _ => StatusCode(500, new { message = "Unable to place the bet right now." })
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Unable to place the bet right now.",
                error = ex.Message
            });
        }
    }

    [HttpPost("{identifier}/bets/resolve")]
    public async Task<IActionResult> ResolveTournamentBets(string identifier)
    {
        var (isAuthenticated, userId, authErrorMessage) = await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request);
        if (!isAuthenticated || string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = authErrorMessage });
        }

        if (!long.TryParse(identifier, out var tournamentId))
        {
            return BadRequest(new { message = "Tournament id is invalid." });
        }

        var isOrganizer = await _bettingService.IsTournamentOrganizerAsync(tournamentId, userId);
        if (!isOrganizer)
        {
            return StatusCode(403, new { message = "Only the organizer can resolve tournament bets." });
        }

        var result = await _bettingService.ResolveTournamentBetsAsync(tournamentId);
        return result.Status switch
        {
            ResolveTournamentBetsStatus.Success => Ok(new
            {
                message = "Tournament bets resolved successfully.",
                bets_won = result.BetsWon,
                bets_lost = result.BetsLost,
                payout_count = result.PayoutCount
            }),
            ResolveTournamentBetsStatus.TournamentNotFound => NotFound(new { message = "Tournament not found." }),
            ResolveTournamentBetsStatus.TournamentNotFinished => BadRequest(new { message = "Tournament is not finished yet." }),
            ResolveTournamentBetsStatus.WinnerNotDefined => BadRequest(new { message = "Tournament winner is not defined yet." }),
            _ => StatusCode(500, new { message = "Unable to resolve tournament bets right now." })
        };
    }

}

public sealed class PlaceTournamentBetBody
{
    [JsonPropertyName("tournamentId")]
    public long TournamentId { get; init; }

    [JsonPropertyName("teamId")]
    public long TeamId { get; init; }

    [JsonPropertyName("coinsBet")]
    public int CoinsBet { get; init; }
}

using Microsoft.AspNetCore.Mvc;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/team-requests")]
public class TeamRequestsController : ControllerBase
{
    private readonly TeamRequestService _teamRequestService;

    public TeamRequestsController(TeamRequestService teamRequestService)
    {
        _teamRequestService = teamRequestService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequestBody request)
    {
        try
        {
            var result = await _teamRequestService.CreateAsync(new CreateTeamRequestCommand
            {
                TournamentId = request.TournamentId,
                CaptainId = request.CaptainId,
                TeamName = request.TeamName,
                PlayerNames = request.PlayerNames
            });

            if (result.Status == CreateTeamRequestStatus.TournamentNotFound)
            {
                return NotFound(new { message = "Tournament not found." });
            }

            if (result.Status == CreateTeamRequestStatus.TournamentFull)
            {
                return BadRequest(new { message = "Tournament is full." });
            }

            if (result.Status == CreateTeamRequestStatus.PendingRequestAlreadyExists)
            {
                return BadRequest(new { message = "Captain already has a pending request for this tournament." });
            }

            if (result.Status == CreateTeamRequestStatus.RejectionLimitReached)
            {
                return BadRequest(new { message = "Captain has already reached the rejection limit for this tournament." });
            }

            if (result.Status == CreateTeamRequestStatus.InvalidPlayerCount)
            {
                return BadRequest(new { message = "Player count does not match players_per_team." });
            }

            return Ok(new
            {
                message = "Team request created successfully.",
                team = result.Team == null
                    ? null
                    : new
                    {
                        id = result.Team.Id,
                        name = result.Team.Name,
                        tournament_id = result.Team.TournamentId,
                        captain_id = result.Team.CaptainId,
                        status = result.Team.Status,
                        rejection_reason = result.Team.RejectionReason,
                        created_at = result.Team.CreatedAt
                    }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Unable to create the team request right now.",
                error = ex.Message
            });
        }
    }

    [HttpPost("admin")]
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminTeamBody request)
    {
        var result = await _teamRequestService.CreateAdminAsync(new CreateAdminTeamCommand
        {
            TournamentId = request.TournamentId,
            CaptainId = request.CaptainId,
            TeamName = request.TeamName,
            PlayerNames = request.PlayerNames
        });

        if (result.Status == CreateAdminTeamStatus.TournamentNotFound)
        {
            return NotFound(new { message = "Tournament not found." });
        }

        if (result.Status == CreateAdminTeamStatus.TournamentFull)
        {
            return BadRequest(new { message = "Tournament is full." });
        }

        if (result.Status == CreateAdminTeamStatus.InvalidPlayerCount)
        {
            return BadRequest(new { message = "Player count does not match players_per_team." });
        }

        return Ok(new
        {
            message = "Accepted team created successfully.",
            team = result.Team == null
                ? null
                : new
                {
                    id = result.Team.Id,
                    name = result.Team.Name,
                    tournament_id = result.Team.TournamentId,
                    captain_id = result.Team.CaptainId,
                    status = result.Team.Status,
                    rejection_reason = result.Team.RejectionReason,
                    created_at = result.Team.CreatedAt
                }
        });
    }

    [HttpPost("{teamId:long}/accept")]
    public async Task<IActionResult> Accept(long teamId)
    {
        var result = await _teamRequestService.ReviewAsync(new ReviewTeamRequestCommand
        {
            TeamId = teamId,
            Status = Models.TeamStatus.Accepted
        });

        if (result.Status == ReviewTeamRequestStatus.TeamNotFound)
        {
            return NotFound(new { message = "Team not found." });
        }

        if (result.Status == ReviewTeamRequestStatus.TournamentNotFound)
        {
            return NotFound(new { message = "Tournament not found." });
        }

        if (result.Status == ReviewTeamRequestStatus.TournamentFull)
        {
            return BadRequest(new { message = "Tournament is full." });
        }

        return Ok(new
        {
            message = "Team accepted successfully.",
            team = result.Team == null
                ? null
                : new
                {
                    id = result.Team.Id,
                    name = result.Team.Name,
                    tournament_id = result.Team.TournamentId,
                    captain_id = result.Team.CaptainId,
                    status = result.Team.Status,
                    rejection_reason = result.Team.RejectionReason,
                    created_at = result.Team.CreatedAt
                },
            current_teams = result.CurrentTeams,
            team_status = result.TournamentTeamStatus
        });
    }

    [HttpPost("{teamId:long}/reject")]
    public async Task<IActionResult> Reject(long teamId, [FromBody] RejectTeamRequestBody request)
    {
        var result = await _teamRequestService.ReviewAsync(new ReviewTeamRequestCommand
        {
            TeamId = teamId,
            Status = Models.TeamStatus.Rejected,
            RejectionReason = request.RejectionReason
        });

        if (result.Status == ReviewTeamRequestStatus.TeamNotFound)
        {
            return NotFound(new { message = "Team not found." });
        }

        if (result.Status == ReviewTeamRequestStatus.TournamentNotFound)
        {
            return NotFound(new { message = "Tournament not found." });
        }

        if (result.Status == ReviewTeamRequestStatus.RejectionReasonRequired)
        {
            return BadRequest(new { message = "Rejection reason is required." });
        }

        return Ok(new
        {
            message = "Team rejected successfully.",
            team = result.Team == null
                ? null
                : new
                {
                    id = result.Team.Id,
                    name = result.Team.Name,
                    tournament_id = result.Team.TournamentId,
                    captain_id = result.Team.CaptainId,
                    status = result.Team.Status,
                    rejection_reason = result.Team.RejectionReason,
                    created_at = result.Team.CreatedAt
                },
            current_teams = result.CurrentTeams,
            team_status = result.TournamentTeamStatus
        });
    }
}

public sealed class CreateTeamRequestBody
{
    public long TournamentId { get; init; }
    public Guid CaptainId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> PlayerNames { get; init; } = Array.Empty<string>();
}

public sealed class RejectTeamRequestBody
{
    public string RejectionReason { get; init; } = string.Empty;
}

public sealed class CreateAdminTeamBody
{
    public long TournamentId { get; init; }
    public Guid CaptainId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> PlayerNames { get; init; } = Array.Empty<string>();
}

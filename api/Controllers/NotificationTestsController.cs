using api.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using PostgrestOperator = Supabase.Postgrest.Constants.Operator;
using Supabase;

namespace api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationTestsController : ControllerBase
{
    private readonly Client _supabase;
    private readonly IDiscordService _discordService;
    private readonly ISupabaseAuthService _supabaseAuthService;

    public NotificationTestsController(
        Client supabase,
        IDiscordService discordService,
        ISupabaseAuthService supabaseAuthService)
    {
        _supabase = supabase;
        _discordService = discordService;
        _supabaseAuthService = supabaseAuthService;
    }

    [HttpPost("test-dm")]
    public async Task<IActionResult> SendTestDm([FromBody] NotificationTestBody request)
    {
        var (isAuthenticated, userId, authErrorMessage) =
            await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request);

        if (!isAuthenticated || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized(new { message = authErrorMessage });
        }

        var profileResponse = await _supabase
            .From<Profile>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, parsedUserId.ToString())
            .Limit(1)
            .Get();

        var profile = profileResponse.Models.FirstOrDefault();
        if (profile == null)
        {
            return NotFound(new { message = "Profile not found." });
        }

        if (string.IsNullOrWhiteSpace(profile.DiscordId))
        {
            return BadRequest(new { message = "No Discord ID found on your profile." });
        }

        var sent = request.Type switch
        {
            "welcome" => await _discordService.SendWelcomeMessageAsync(
                profile.DiscordId,
                profile.Username ?? profile.Email ?? "speler"),
            "join_request" => await _discordService.SendJoinRequestNotificationAsync(
                profile.DiscordId,
                "BracketHub Test Team",
                profile.Username ?? "Test Captain",
                "http://localhost:5173/tournament/test/admin/teams"),
            "join_accept" => await _discordService.SendJoinRequestResponseAsync(
                profile.DiscordId,
                true,
                "BracketHub Invitational",
                DateTime.UtcNow.AddDays(2),
                null),
            "join_reject" => await _discordService.SendJoinRequestResponseAsync(
                profile.DiscordId,
                false,
                "BracketHub Invitational",
                DateTime.UtcNow.AddDays(2),
                "Dit is een testweigering vanuit het admin testpaneel."),
            "reminder" => await _discordService.SendTournamentReminderAsync(
                profile.DiscordId,
                "BracketHub Invitational",
                "http://localhost:5173/tournament/brackethub-invitational"),
            "bet_won" => await _discordService.SendBetResultAsync(
                profile.DiscordId,
                true,
                "BracketHub Invitational: Team Alpha 2 - 0 Team Omega",
                250),
            "bet_lost" => await _discordService.SendBetResultAsync(
                profile.DiscordId,
                false,
                "BracketHub Invitational: Team Alpha 2 - 0 Team Omega",
                100),
            "reward" => await _discordService.SendRewardDeliveryAsync(
                profile.DiscordId,
                "Steam Gift Card 10 EUR",
                "BH-TEST-2026-STEAM"),
            _ => false
        };

        if (!sent)
        {
            return BadRequest(new
            {
                message = "Discord DM kon niet verzonden worden. Controleer je bot token, gedeelde server en DM-instellingen.",
                discord_id = profile.DiscordId
            });
        }

        return Ok(new
        {
            message = "Test DM verzonden.",
            discord_id = profile.DiscordId,
            type = request.Type
        });
    }
}

public sealed class NotificationTestBody
{
    public string Type { get; init; } = string.Empty;
}

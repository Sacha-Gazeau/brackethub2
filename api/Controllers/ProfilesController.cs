using api.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using PostgrestOperator = Supabase.Postgrest.Constants.Operator;
using PostgrestQueryOptions = Supabase.Postgrest.QueryOptions;
using Supabase;

namespace api.Controllers;

[ApiController]
[Route("api/profiles")]
public class ProfilesController : ControllerBase
{
    private readonly Client _supabase;
    private readonly IDiscordService _discordService;
    private readonly ISupabaseAuthService _supabaseAuthService;
    private readonly ILogger<ProfilesController> _logger;

    public ProfilesController(
        Client supabase,
        IDiscordService discordService,
        ISupabaseAuthService supabaseAuthService,
        ILogger<ProfilesController> logger)
    {
        _supabase = supabase;
        _discordService = discordService;
        _supabaseAuthService = supabaseAuthService;
        _logger = logger;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncProfileBody request)
    {
        var existingResponse = await _supabase
            .From<Profile>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, request.Id.ToString())
            .Limit(1)
            .Get();

        var profile = existingResponse.Models.FirstOrDefault();
        var isNewProfile = profile == null;
        var existingDiscordId = profile?.DiscordId;

        if (profile == null)
        {
            profile = new Profile
            {
                Id = request.Id,
                Coins = 0,
                LifetimeCoins = 0
            };
        }

        profile.Email = request.Email;
        profile.Username = request.Username;
        profile.Avatar = request.Avatar;
        profile.DiscordId = request.DiscordId;
        profile.IsInServer = await ResolveGuildMembershipAsync(profile.DiscordId);

        if (isNewProfile)
        {
            await _supabase.From<Profile>().Insert(profile, new PostgrestQueryOptions());
        }
        else
        {
            await profile.Update<Profile>();
        }

        var shouldSendWelcome =
            !string.IsNullOrWhiteSpace(profile.DiscordId) &&
            (isNewProfile || string.IsNullOrWhiteSpace(existingDiscordId));

        var discordId = profile.DiscordId;
        if (shouldSendWelcome && !string.IsNullOrWhiteSpace(discordId))
        {
            var displayName = profile.Username ?? profile.Email ?? "speler";
            var sent = await _discordService.SendWelcomeMessageAsync(discordId, displayName);
            if (!sent)
            {
                _logger.LogInformation(
                    "Welcome DM could not be delivered to Discord user {DiscordId}.",
                    discordId);
            }
        }

        return Ok(new
        {
            message = "Profile synced successfully.",
            created = isNewProfile,
            discord_id = profile.DiscordId,
            is_in_server = profile.IsInServer,
            welcome_attempted = shouldSendWelcome
        });
    }

    [HttpPost("{id:guid}/sync-guild-status")]
    public async Task<IActionResult> SyncGuildStatus(Guid id)
    {
        var response = await _supabase
            .From<Profile>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, id.ToString())
            .Limit(1)
            .Get();

        var profile = response.Models.FirstOrDefault();
        if (profile is null)
        {
            return NotFound(new { message = "Profile not found." });
        }

        profile.IsInServer = await ResolveGuildMembershipAsync(profile.DiscordId);
        await profile.Update<Profile>();

        return Ok(new
        {
            message = "Guild membership synced successfully.",
            profile_id = profile.Id,
            discord_id = profile.DiscordId,
            is_in_server = profile.IsInServer
        });
    }

    [HttpPost("me/recheck-server")]
    public async Task<IActionResult> RecheckCurrentUserServerStatus(CancellationToken cancellationToken)
    {
        var (isAuthenticated, userId, authErrorMessage) =
            await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request, cancellationToken);

        if (!isAuthenticated || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized(new { message = authErrorMessage });
        }

        var response = await _supabase
            .From<Profile>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, parsedUserId.ToString())
            .Limit(1)
            .Get(cancellationToken);

        var profile = response.Models.FirstOrDefault();
        if (profile is null)
        {
            return NotFound(new { message = "Profile not found." });
        }

        profile.IsInServer = await ResolveGuildMembershipAsync(profile.DiscordId);
        await profile.Update<Profile>(cancellationToken: cancellationToken);

        return Ok(new
        {
            message = "Guild membership rechecked successfully.",
            profile_id = profile.Id,
            discord_id = profile.DiscordId,
            is_in_server = profile.IsInServer
        });
    }

    private async Task<bool> ResolveGuildMembershipAsync(string? discordId)
    {
        if (string.IsNullOrWhiteSpace(discordId) || !ulong.TryParse(discordId, out var discordUserId))
        {
            return false;
        }

        return await _discordService.IsUserInGuildAsync(discordUserId);
    }
}

public sealed class SyncProfileBody
{
    public Guid Id { get; init; }
    public string? Email { get; init; }
    public string? Username { get; init; }
    public string? Avatar { get; init; }
    public string? DiscordId { get; init; }
}

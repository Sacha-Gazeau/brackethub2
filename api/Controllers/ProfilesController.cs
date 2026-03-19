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
        try
        {
            var existingResponse = await _supabase
                .From<Profile>()
                .Select("*")
                .Filter("id", PostgrestOperator.Equals, request.Id.ToString())
                .Limit(1)
                .Get();

            var profile = existingResponse.Models.FirstOrDefault();
            var isNewProfile = profile == null;
            var wasInServer = profile?.IsInServer == true;

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

            var shouldSendWelcome = await TrySendWelcomeMessageAsync(profile, wasInServer);

            return Ok(new
            {
                message = "Profile synced successfully.",
                created = isNewProfile,
                discord_id = profile.DiscordId,
                is_in_server = profile.IsInServer,
                welcome_attempted = shouldSendWelcome
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile sync failed for user {UserId}.", request.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Profile sync failed.",
                detail = ex.Message
            });
        }
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

        var wasInServer = profile.IsInServer == true;
        profile.IsInServer = await ResolveGuildMembershipAsync(profile.DiscordId);
        await profile.Update<Profile>();
        await TrySendWelcomeMessageAsync(profile, wasInServer);

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
        try
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

            var wasInServer = profile.IsInServer == true;
            profile.IsInServer = await ResolveGuildMembershipAsync(profile.DiscordId);
            await profile.Update<Profile>(cancellationToken: cancellationToken);
            await TrySendWelcomeMessageAsync(profile, wasInServer);

            return Ok(new
            {
                message = "Guild membership rechecked successfully.",
                profile_id = profile.Id,
                discord_id = profile.DiscordId,
                is_in_server = profile.IsInServer
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guild membership recheck failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Guild membership recheck failed.",
                detail = ex.Message
            });
        }
    }

    private async Task<bool> ResolveGuildMembershipAsync(string? discordId)
    {
        if (string.IsNullOrWhiteSpace(discordId) || !ulong.TryParse(discordId, out var discordUserId))
        {
            _logger.LogWarning("Skipping guild membership check because DiscordId is empty or invalid: {DiscordId}", discordId);
            return false;
        }

        _logger.LogInformation("Checking guild membership for Discord user {DiscordUserId}.", discordUserId);
        return await _discordService.IsUserInGuildAsync(discordUserId);
    }

    private async Task<bool> TrySendWelcomeMessageAsync(Profile profile, bool wasInServer)
    {
        if (wasInServer || profile.IsInServer != true || string.IsNullOrWhiteSpace(profile.DiscordId))
        {
            return false;
        }

        var displayName = profile.Username ?? profile.Email ?? "speler";
        var sent = await _discordService.SendWelcomeMessageAsync(profile.DiscordId, displayName);
        if (!sent)
        {
            _logger.LogInformation(
                "Welcome DM could not be delivered to Discord user {DiscordId}.",
                profile.DiscordId);
        }

        return true;
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

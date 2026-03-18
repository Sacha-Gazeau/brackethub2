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
    private readonly ILogger<ProfilesController> _logger;

    public ProfilesController(
        Client supabase,
        IDiscordService discordService,
        ILogger<ProfilesController> logger)
    {
        _supabase = supabase;
        _discordService = discordService;
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
            welcome_attempted = shouldSendWelcome
        });
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

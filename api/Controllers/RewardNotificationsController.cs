using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/rewards")]
public class RewardNotificationsController : ControllerBase
{
    private readonly IDiscordService _discordService;

    public RewardNotificationsController(IDiscordService discordService)
    {
        _discordService = discordService;
    }

    [HttpPost("deliver")]
    public async Task<IActionResult> Deliver([FromBody] RewardDeliveryBody request)
    {
        var sent = await _discordService.SendRewardDeliveryAsync(
            request.UserDiscordId,
            request.RewardName,
            request.RewardCode);

        return Ok(new
        {
            message = sent
                ? "Reward delivery DM sent."
                : "Reward delivery completed, but the Discord DM could not be delivered."
        });
    }
}

public sealed class RewardDeliveryBody
{
    public string UserDiscordId { get; init; } = string.Empty;
    public string RewardName { get; init; } = string.Empty;
    public string RewardCode { get; init; } = string.Empty;
}

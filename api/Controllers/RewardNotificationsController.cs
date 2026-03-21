using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/rewards")]
public class RewardNotificationsController : ControllerBase
{
    private readonly IDiscordService _discordService;
    private readonly IAppTextService _text;

    public RewardNotificationsController(
        IDiscordService discordService,
        IAppTextService text)
    {
        _discordService = discordService;
        _text = text;
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
                ? _text.Get("backendMessages.rewards.deliverySent")
                : _text.Get("backendMessages.rewards.deliveryFailed")
        });
    }
}

public sealed class RewardDeliveryBody
{
    public string UserDiscordId { get; init; } = string.Empty;
    public string RewardName { get; init; } = string.Empty;
    public string RewardCode { get; init; } = string.Empty;
}

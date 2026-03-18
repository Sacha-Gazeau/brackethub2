using Microsoft.AspNetCore.Mvc;
using api.Models;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DailyRewardController : ControllerBase
{
    private readonly DailyRewardService _dailyRewardService;

    public DailyRewardController(DailyRewardService dailyRewardService)
    {
        _dailyRewardService = dailyRewardService;
    }

    [HttpPost("claim")]
    public async Task<IActionResult> ClaimDaily([FromBody] ClaimDailyRequest request)
    {
        var result = await _dailyRewardService.ClaimDailyAsync(request.UserId);

        if (result.Status == ClaimDailyStatus.NotFound)
        {
            return NotFound(new { message = "Profile not found" });
        }

        if (result.Status == ClaimDailyStatus.AlreadyClaimed)
        {
            return Ok(new
            {
                message = "Daily reward already claimed today",
                alreadyClaimed = true,
                coins = result.Coins,
                lastDailyClaim = result.LastDailyClaim
            });
        }

        return Ok(new
        {
            message = "Daily reward claimed successfully",
            alreadyClaimed = false,
            coins = result.Coins,
            lastDailyClaim = result.LastDailyClaim
        });
    }
}

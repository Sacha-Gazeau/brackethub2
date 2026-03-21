using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/rewards")]
public class RewardsController : ControllerBase
{
    private readonly RewardService _rewardService;
    private readonly ISupabaseAuthService _supabaseAuthService;
    private readonly ILogger<RewardsController> _logger;

    public RewardsController(
        RewardService rewardService,
        ISupabaseAuthService supabaseAuthService,
        ILogger<RewardsController> logger)
    {
        _rewardService = rewardService;
        _supabaseAuthService = supabaseAuthService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRewards(CancellationToken cancellationToken)
    {
        try
        {
            var authResult = await TryGetAuthenticatedUserIdAsync(cancellationToken);
            if (!authResult.Succeeded)
            {
                return authResult.FailureResult!;
            }

            var result = await _rewardService.GetRewardCatalogAsync(authResult.UserId, cancellationToken);
            if (result.Status == RewardCatalogStatus.ProfileNotFound)
            {
                return NotFound(CreateErrorPayload("profile_not_found", "Profiel niet gevonden."));
            }

            return Ok(CreateCatalogPayload(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rewards catalog.");
            return CreateErrorResult(
                StatusCodes.Status500InternalServerError,
                "reward_catalog_failed",
                "De rewards-catalogus kon niet geladen worden.",
                ex.Message);
        }
    }

    [HttpPost("{id:long}/purchase")]
    public async Task<IActionResult> PurchaseReward(long id, CancellationToken cancellationToken)
    {
        try
        {
            var authResult = await TryGetAuthenticatedUserIdAsync(cancellationToken);
            if (!authResult.Succeeded)
            {
                return authResult.FailureResult!;
            }

            var result = await _rewardService.PurchaseRewardAsync(authResult.UserId, id, cancellationToken);
            return CreatePurchaseActionResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled reward purchase failure for reward {RewardId}.", id);
            return CreateErrorResult(
                StatusCodes.Status500InternalServerError,
                "reward_purchase_failed",
                "De aankoop kon niet worden voltooid.",
                ex.Message);
        }
    }

    private async Task<(bool Succeeded, Guid UserId, IActionResult? FailureResult)> TryGetAuthenticatedUserIdAsync(
        CancellationToken cancellationToken)
    {
        var (isAuthenticated, userId, authErrorMessage) =
            await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request, cancellationToken);

        if (!isAuthenticated || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsedUserId))
        {
            return (false, Guid.Empty, Unauthorized(new { message = authErrorMessage }));
        }

        return (true, parsedUserId, null);
    }

    private IActionResult CreatePurchaseActionResult(RewardPurchaseResult result) =>
        result.Status switch
        {
            RewardPurchaseStatus.Success => Ok(new
            {
                message = GetPurchaseSuccessMessage(result.RewardType),
                reward_id = result.RewardId,
                reward_type = result.RewardType,
                reward_name = result.RewardName,
                remaining_coins = result.RemainingCoins
            }),
            RewardPurchaseStatus.ProfileNotFound => CreateErrorResult(StatusCodes.Status404NotFound, "profile_not_found", "Profiel niet gevonden."),
            RewardPurchaseStatus.RewardNotFound => CreateErrorResult(StatusCodes.Status404NotFound, "reward_not_found", "Reward niet gevonden."),
            RewardPurchaseStatus.RewardInactive => CreateErrorResult(StatusCodes.Status400BadRequest, "reward_inactive", "Deze reward is niet beschikbaar."),
            RewardPurchaseStatus.UnsupportedRewardType => CreateErrorResult(StatusCodes.Status400BadRequest, "unsupported_reward_type", "Dit rewardtype wordt niet ondersteund."),
            RewardPurchaseStatus.DiscordAccountRequired => CreateErrorResult(StatusCodes.Status400BadRequest, "discord_account_required", "Discord-account vereist."),
            RewardPurchaseStatus.InsufficientCoins => CreateErrorResult(StatusCodes.Status400BadRequest, "insufficient_coins", "Je hebt niet genoeg coins."),
            RewardPurchaseStatus.NoCodesAvailable => CreateErrorResult(StatusCodes.Status409Conflict, "reward_out_of_stock", "Niet beschikbaar."),
            RewardPurchaseStatus.AlreadyPurchased => CreateErrorResult(StatusCodes.Status409Conflict, "reward_already_owned", "Je hebt deze rol al"),
            RewardPurchaseStatus.DiscordRoleNotConfigured => CreateErrorResult(StatusCodes.Status500InternalServerError, "discord_role_not_configured", "Deze reward is nog niet volledig ingesteld."),
            RewardPurchaseStatus.DiscordDmFailed => CreateErrorResult(StatusCodes.Status400BadRequest, "discord_dm_failed", "Je Discord privéberichten moeten openstaan", result.FailureDetail),
            RewardPurchaseStatus.DiscordRoleAssignmentFailed => CreateErrorResult(StatusCodes.Status502BadGateway, "discord_role_assignment_failed", "De Discord-rol kon niet worden toegekend.", result.FailureDetail),
            RewardPurchaseStatus.CompensationFailed => CreateErrorResult(StatusCodes.Status500InternalServerError, "reward_compensation_failed", "De aankoop kon niet volledig worden afgerond. Neem contact op met support.", result.FailureDetail),
            _ => CreateErrorResult(StatusCodes.Status500InternalServerError, "reward_purchase_failed", "De aankoop kon niet worden voltooid.")
        };

    private static object CreateCatalogPayload(RewardCatalogResult result) => new
    {
        coins_balance = result.CoinsBalance,
        discord_account_linked = result.DiscordAccountLinked,
        rewards = result.Rewards.Select(reward => new
        {
            id = reward.Id,
            name = reward.Name,
            description = reward.Description,
            type = reward.Type,
            price_coins = reward.PriceCoins,
            stock = reward.Stock,
            already_purchased = reward.AlreadyPurchased,
            is_available = reward.IsAvailable
        })
    };

    private IActionResult CreateErrorResult(
        int statusCode,
        string code,
        string message,
        string? detail = null) =>
        StatusCode(statusCode, CreateErrorPayload(code, message, detail));

    private static object CreateErrorPayload(
        string code,
        string message,
        string? detail = null) =>
        string.IsNullOrWhiteSpace(detail)
            ? new { code, message }
            : new { code, message, detail };

    private static string GetPurchaseSuccessMessage(string rewardType) =>
        string.Equals(rewardType, RewardTypes.SubscriptionCode, StringComparison.Ordinal)
            ? "Reward gekocht. De code wordt via DM verstuurd."
            : "Reward gekocht. Je Discord-rol werd toegevoegd.";
}

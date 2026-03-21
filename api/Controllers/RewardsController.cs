using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

[ApiController]
[Route("api/rewards")]
public class RewardsController : ControllerBase
{
    private readonly RewardService _rewardService;
    private readonly ISupabaseAuthService _supabaseAuthService;
    private readonly IAppTextService _text;
    private readonly ILogger<RewardsController> _logger;

    public RewardsController(
        RewardService rewardService,
        ISupabaseAuthService supabaseAuthService,
        IAppTextService text,
        ILogger<RewardsController> logger)
    {
        _rewardService = rewardService;
        _supabaseAuthService = supabaseAuthService;
        _text = text;
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
                return CreateErrorResult(StatusCodes.Status404NotFound, "profile_not_found", "shopPage.errors.profileNotFound");
            }

            return Ok(CreateCatalogPayload(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rewards catalog.");
            return CreateErrorResult(
                StatusCodes.Status500InternalServerError,
                "reward_catalog_failed",
                "shopPage.errors.catalogLoadFailed");
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
                "shopPage.errors.purchaseFailed");
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
                code = GetPurchaseSuccessCode(result.RewardType),
                message = GetPurchaseSuccessMessage(result.RewardType),
                reward_id = result.RewardId,
                reward_type = result.RewardType,
                reward_name = result.RewardName,
                remaining_coins = result.RemainingCoins
            }),
            RewardPurchaseStatus.ProfileNotFound => CreateErrorResult(StatusCodes.Status404NotFound, "profile_not_found", "shopPage.errors.profileNotFound"),
            RewardPurchaseStatus.RewardNotFound => CreateErrorResult(StatusCodes.Status404NotFound, "reward_not_found", "shopPage.errors.rewardNotFound"),
            RewardPurchaseStatus.RewardInactive => CreateErrorResult(StatusCodes.Status400BadRequest, "reward_inactive", "shopPage.errors.rewardInactive"),
            RewardPurchaseStatus.UnsupportedRewardType => CreateErrorResult(StatusCodes.Status400BadRequest, "unsupported_reward_type", "shopPage.errors.unsupportedRewardType"),
            RewardPurchaseStatus.DiscordAccountRequired => CreateErrorResult(StatusCodes.Status400BadRequest, "discord_account_required", "shopPage.common.discordAccountRequired"),
            RewardPurchaseStatus.InsufficientCoins => CreateErrorResult(StatusCodes.Status400BadRequest, "insufficient_coins", "shopPage.errors.insufficientCoins"),
            RewardPurchaseStatus.NoCodesAvailable => CreateErrorResult(StatusCodes.Status409Conflict, "reward_out_of_stock", "shopPage.actions.unavailable"),
            RewardPurchaseStatus.AlreadyPurchased => CreateErrorResult(StatusCodes.Status409Conflict, "reward_already_owned", "shopPage.actions.alreadyOwned"),
            RewardPurchaseStatus.DiscordRoleNotConfigured => CreateErrorResult(StatusCodes.Status500InternalServerError, "discord_role_not_configured", "shopPage.errors.discordRoleNotConfigured"),
            RewardPurchaseStatus.DiscordDmFailed => CreateErrorResult(StatusCodes.Status400BadRequest, "discord_dm_failed", "shopPage.common.dmsOpen"),
            RewardPurchaseStatus.DiscordRoleAssignmentFailed => CreateErrorResult(StatusCodes.Status502BadGateway, "discord_role_assignment_failed", "shopPage.errors.roleAssignmentFailed"),
            RewardPurchaseStatus.CompensationFailed => CreateErrorResult(StatusCodes.Status500InternalServerError, "reward_compensation_failed", "shopPage.errors.compensationFailed"),
            _ => CreateErrorResult(StatusCodes.Status500InternalServerError, "reward_purchase_failed", "shopPage.errors.purchaseFailed")
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
        string messageKey) =>
        StatusCode(statusCode, CreateErrorPayload(code, _text.Get(messageKey)));

    private static object CreateErrorPayload(
        string code,
        string message) =>
        new { code, message };

    private string GetPurchaseSuccessMessage(string rewardType) =>
        _text.Get(GetPurchaseSuccessMessageKey(rewardType));

    private static string GetPurchaseSuccessCode(string rewardType) =>
        string.Equals(rewardType, RewardTypes.SubscriptionCode, StringComparison.Ordinal)
            ? "subscription_code_purchased"
            : "discord_role_purchased";

    private static string GetPurchaseSuccessMessageKey(string rewardType) =>
        string.Equals(rewardType, RewardTypes.SubscriptionCode, StringComparison.Ordinal)
            ? "shopPage.feedback.subscriptionPurchased"
            : "shopPage.feedback.rolePurchased";
}

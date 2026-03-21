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
            var (isAuthenticated, userId, authErrorMessage) =
                await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request, cancellationToken);

            if (!isAuthenticated || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized(new { message = authErrorMessage });
            }

            var result = await _rewardService.GetRewardCatalogAsync(parsedUserId, cancellationToken);
            if (result.Status == RewardCatalogStatus.ProfileNotFound)
            {
                return NotFound(new { message = "Profile not found." });
            }

            return Ok(new
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
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rewards catalog.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                code = "reward_catalog_failed",
                message = "De rewards-catalogus kon niet geladen worden.",
                detail = ex.Message
            });
        }
    }

    [HttpPost("{id:long}/purchase")]
    public async Task<IActionResult> PurchaseReward(long id, CancellationToken cancellationToken)
    {
        try
        {
            var (isAuthenticated, userId, authErrorMessage) =
                await _supabaseAuthService.TryGetAuthenticatedUserIdAsync(Request, cancellationToken);

            if (!isAuthenticated || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized(new { message = authErrorMessage });
            }

            var result = await _rewardService.PurchaseRewardAsync(parsedUserId, id, cancellationToken);

            return result.Status switch
            {
                RewardPurchaseStatus.Success => Ok(new
                {
                    message = string.Equals(result.RewardType, RewardTypes.SubscriptionCode, StringComparison.Ordinal)
                        ? "Reward gekocht. De code wordt via DM verstuurd."
                        : "Reward gekocht. Je Discord-rol werd toegevoegd.",
                    reward_id = result.RewardId,
                    reward_type = result.RewardType,
                    reward_name = result.RewardName,
                    remaining_coins = result.RemainingCoins
                }),
                RewardPurchaseStatus.ProfileNotFound => NotFound(new { code = "profile_not_found", message = "Profiel niet gevonden." }),
                RewardPurchaseStatus.RewardNotFound => NotFound(new { code = "reward_not_found", message = "Reward niet gevonden." }),
                RewardPurchaseStatus.RewardInactive => BadRequest(new { code = "reward_inactive", message = "Deze reward is niet beschikbaar." }),
                RewardPurchaseStatus.UnsupportedRewardType => BadRequest(new { code = "unsupported_reward_type", message = "Dit rewardtype wordt niet ondersteund." }),
                RewardPurchaseStatus.DiscordAccountRequired => BadRequest(new { code = "discord_account_required", message = "Discord-account vereist." }),
                RewardPurchaseStatus.InsufficientCoins => BadRequest(new { code = "insufficient_coins", message = "Je hebt niet genoeg coins." }),
                RewardPurchaseStatus.NoCodesAvailable => Conflict(new { code = "reward_out_of_stock", message = "Niet beschikbaar." }),
                RewardPurchaseStatus.AlreadyPurchased => Conflict(new { code = "reward_already_owned", message = "Je hebt deze rol al" }),
                RewardPurchaseStatus.DiscordRoleNotConfigured => StatusCode(StatusCodes.Status500InternalServerError, new { code = "discord_role_not_configured", message = "Deze reward is nog niet volledig ingesteld." }),
                RewardPurchaseStatus.DatabaseConfigurationMissing => StatusCode(StatusCodes.Status500InternalServerError, new { code = "database_configuration_missing", message = "De rewards-backend mist een PostgreSQL connectiestring.", detail = result.FailureDetail }),
                RewardPurchaseStatus.DatabaseConnectionFailed => StatusCode(StatusCodes.Status500InternalServerError, new { code = "database_connection_failed", message = "De rewards-database kon niet worden bereikt.", detail = result.FailureDetail }),
                RewardPurchaseStatus.DiscordDmFailed => BadRequest(new { code = "discord_dm_failed", message = "Je Discord privéberichten moeten openstaan", detail = result.FailureDetail }),
                RewardPurchaseStatus.DiscordRoleAssignmentFailed => StatusCode(StatusCodes.Status502BadGateway, new { code = "discord_role_assignment_failed", message = "De Discord-rol kon niet worden toegekend.", detail = result.FailureDetail }),
                RewardPurchaseStatus.CompensationFailed => StatusCode(StatusCodes.Status500InternalServerError, new { code = "reward_compensation_failed", message = "De aankoop kon niet volledig worden afgerond. Neem contact op met support.", detail = result.FailureDetail }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { code = "reward_purchase_failed", message = "De aankoop kon niet worden voltooid." })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled reward purchase failure for reward {RewardId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                code = "reward_purchase_failed",
                message = "De aankoop kon niet worden voltooid.",
                detail = ex.Message
            });
        }
    }
}

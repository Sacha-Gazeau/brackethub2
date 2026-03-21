using api.Models;
using PostgrestOperator = Supabase.Postgrest.Constants.Operator;
using PostgrestOrdering = Supabase.Postgrest.Constants.Ordering;
using PostgrestQueryOptions = Supabase.Postgrest.QueryOptions;
using SupabaseClient = Supabase.Client;

namespace api.Services;

public static class RewardTypes
{
    public const string SubscriptionCode = "subscription_code";
    public const string DiscordRole = "discord_role";
}

public enum RewardCatalogStatus
{
    Success,
    ProfileNotFound
}

public sealed class RewardCatalogResult
{
    public RewardCatalogStatus Status { get; init; }
    public int CoinsBalance { get; init; }
    public bool DiscordAccountLinked { get; init; }
    public IReadOnlyList<RewardCatalogItem> Rewards { get; init; } = Array.Empty<RewardCatalogItem>();
}

public sealed class RewardCatalogItem
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int PriceCoins { get; init; }
    public int? Stock { get; init; }
    public bool AlreadyPurchased { get; init; }
    public bool IsAvailable { get; init; }
}

public enum RewardPurchaseStatus
{
    Success,
    ProfileNotFound,
    RewardNotFound,
    RewardInactive,
    UnsupportedRewardType,
    DiscordAccountRequired,
    InsufficientCoins,
    NoCodesAvailable,
    AlreadyPurchased,
    DiscordRoleNotConfigured,
    DiscordDmFailed,
    DiscordRoleAssignmentFailed,
    CompensationFailed
}

public sealed class RewardPurchaseResult
{
    public RewardPurchaseStatus Status { get; init; }
    public long RewardId { get; init; }
    public string RewardName { get; init; } = string.Empty;
    public string RewardType { get; init; } = string.Empty;
    public int? RemainingCoins { get; init; }
    public string? FailureDetail { get; init; }
}

public class RewardService
{
    private const int MaxRewardCodeReservationAttempts = 5;

    private readonly SupabaseClient _supabase;
    private readonly IDiscordService _discordService;
    private readonly ILogger<RewardService> _logger;

    public RewardService(
        SupabaseClient supabase,
        IDiscordService discordService,
        ILogger<RewardService> logger)
    {
        _supabase = supabase;
        _discordService = discordService;
        _logger = logger;
    }

    public async Task<RewardCatalogResult> GetRewardCatalogAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return new RewardCatalogResult
            {
                Status = RewardCatalogStatus.ProfileNotFound
            };
        }

        var rewardResponse = await _supabase
            .From<Reward>()
            .Select("*")
            .Filter("is_active", PostgrestOperator.Equals, "true")
            .Get(cancellationToken);

        var rewards = rewardResponse.Models
            .OrderBy(GetTypeSortOrder)
            .ThenBy(reward => reward.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var purchasedRewardsResponse = await _supabase
            .From<UserReward>()
            .Select("*")
            .Filter("user_id", PostgrestOperator.Equals, userId.ToString())
            .Get(cancellationToken);

        var purchasedRoleRewardIds = purchasedRewardsResponse.Models
            .Select(model => model.RewardId)
            .ToHashSet();

        var availableCodeCountsByRewardId = new Dictionary<long, int>();
        if (rewards.Any(reward => string.Equals(reward.Type, RewardTypes.SubscriptionCode, StringComparison.Ordinal)))
        {
            var rewardCodesResponse = await _supabase
                .From<RewardCode>()
                .Select("*")
                .Filter("is_used", PostgrestOperator.Equals, "false")
                .Get(cancellationToken);

            availableCodeCountsByRewardId = rewardCodesResponse.Models
                .GroupBy(code => code.RewardId)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        var items = rewards.Select(reward =>
        {
            var isSubscription = string.Equals(reward.Type, RewardTypes.SubscriptionCode, StringComparison.Ordinal);
            var stock = isSubscription
                ? availableCodeCountsByRewardId.GetValueOrDefault(reward.Id)
                : reward.Stock;
            var alreadyPurchased = !isSubscription && purchasedRoleRewardIds.Contains(reward.Id);
            var isAvailable = isSubscription
                ? stock.GetValueOrDefault() > 0
                : !alreadyPurchased;

            return new RewardCatalogItem
            {
                Id = reward.Id,
                Name = reward.Name,
                Description = reward.Description,
                Type = reward.Type,
                PriceCoins = reward.PriceCoins,
                Stock = isSubscription ? stock : reward.Stock,
                AlreadyPurchased = alreadyPurchased,
                IsAvailable = isAvailable
            };
        }).ToList();

        return new RewardCatalogResult
        {
            Status = RewardCatalogStatus.Success,
            CoinsBalance = profile.Coins,
            DiscordAccountLinked = !string.IsNullOrWhiteSpace(profile.DiscordId),
            Rewards = items
        };
    }

    public async Task<RewardPurchaseResult> PurchaseRewardAsync(
        Guid userId,
        long rewardId,
        CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return new RewardPurchaseResult
            {
                Status = RewardPurchaseStatus.ProfileNotFound,
                RewardId = rewardId
            };
        }

        var reward = await GetRewardAsync(rewardId, cancellationToken);
        if (reward is null)
        {
            return new RewardPurchaseResult
            {
                Status = RewardPurchaseStatus.RewardNotFound,
                RewardId = rewardId
            };
        }

        if (!reward.IsActive)
        {
            return BuildPurchaseResult(
                RewardPurchaseStatus.RewardInactive,
                reward,
                profile.Coins);
        }

        if (string.IsNullOrWhiteSpace(profile.DiscordId))
        {
            return BuildPurchaseResult(
                RewardPurchaseStatus.DiscordAccountRequired,
                reward,
                profile.Coins);
        }

        return reward.Type switch
        {
            RewardTypes.SubscriptionCode => await PurchaseSubscriptionCodeAsync(profile, reward, cancellationToken),
            RewardTypes.DiscordRole => await PurchaseDiscordRoleAsync(profile, reward, cancellationToken),
            _ => BuildPurchaseResult(
                RewardPurchaseStatus.UnsupportedRewardType,
                reward,
                profile.Coins)
        };
    }

    private static int GetTypeSortOrder(Reward reward) => reward.Type switch
    {
        RewardTypes.SubscriptionCode => 0,
        RewardTypes.DiscordRole => 1,
        _ => 2
    };

    private async Task<RewardPurchaseResult> PurchaseSubscriptionCodeAsync(
        Profile profile,
        Reward reward,
        CancellationToken cancellationToken)
    {
        var reservedCode = await TryReserveRewardCodeAsync(reward.Id, profile.Id, cancellationToken);
        if (reservedCode is null)
        {
            return BuildPurchaseResult(
                RewardPurchaseStatus.NoCodesAvailable,
                reward,
                profile.Coins);
        }

        var preparation = await TryPreparePurchaseAsync(profile, reward, cancellationToken);
        if (!preparation.Succeeded)
        {
            await ReleaseRewardCodeAsync(reservedCode.Id, cancellationToken);
            return BuildPurchaseResult(
                RewardPurchaseStatus.InsufficientCoins,
                reward,
                preparation.CurrentCoins);
        }

        long? userRewardId = null;
        try
        {
            userRewardId = await InsertUserRewardAsync(profile.Id, reward.Id, cancellationToken);

            var dmMessage = BuildSubscriptionCodeMessage(reward.Name, reservedCode.CodeValue);
            var dmResult = await _discordService.SendDirectMessageAsync(profile.DiscordId!, dmMessage, cancellationToken);
            if (dmResult.Success)
            {
                return BuildPurchaseResult(
                    RewardPurchaseStatus.Success,
                    reward,
                    preparation.RemainingCoins);
            }

            var compensationSucceeded = await TryCompensatePurchaseAsync(
                profile.Id,
                preparation.PreviousCoins,
                userRewardId,
                reservedCode.Id,
                reward.Type,
                cancellationToken);

            return BuildCompensatedFailureResult(
                compensationSucceeded,
                RewardPurchaseStatus.DiscordDmFailed,
                reward,
                preparation.PreviousCoins,
                dmResult.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscription reward purchase failed. RewardId: {RewardId}, UserId: {UserId}", reward.Id, profile.Id);

            var compensationSucceeded = await TryCompensatePurchaseAsync(
                profile.Id,
                preparation.PreviousCoins,
                userRewardId,
                reservedCode.Id,
                reward.Type,
                cancellationToken);

            return BuildCompensatedFailureResult(
                compensationSucceeded,
                RewardPurchaseStatus.CompensationFailed,
                reward,
                preparation.PreviousCoins,
                ex.Message);
        }
    }

    private async Task<RewardPurchaseResult> PurchaseDiscordRoleAsync(
        Profile profile,
        Reward reward,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reward.DiscordRoleId))
        {
            return BuildPurchaseResult(
                RewardPurchaseStatus.DiscordRoleNotConfigured,
                reward,
                profile.Coins);
        }

        var alreadyPurchased = await UserAlreadyOwnsRewardAsync(profile.Id, reward.Id, cancellationToken);
        if (alreadyPurchased)
        {
            return BuildPurchaseResult(
                RewardPurchaseStatus.AlreadyPurchased,
                reward,
                profile.Coins);
        }

        var preparation = await TryPreparePurchaseAsync(profile, reward, cancellationToken);
        if (!preparation.Succeeded)
        {
            return BuildPurchaseResult(
                RewardPurchaseStatus.InsufficientCoins,
                reward,
                preparation.CurrentCoins);
        }

        long? userRewardId = null;
        try
        {
            userRewardId = await InsertUserRewardAsync(profile.Id, reward.Id, cancellationToken);

            var roleResult = await _discordService.AddRoleToUserAsync(
                profile.DiscordId!,
                reward.DiscordRoleId,
                cancellationToken);

            if (roleResult.Success)
            {
                return BuildPurchaseResult(
                    RewardPurchaseStatus.Success,
                    reward,
                    preparation.RemainingCoins);
            }

            var compensationSucceeded = await TryCompensatePurchaseAsync(
                profile.Id,
                preparation.PreviousCoins,
                userRewardId,
                rewardCodeId: null,
                reward.Type,
                cancellationToken);

            return BuildCompensatedFailureResult(
                compensationSucceeded,
                RewardPurchaseStatus.DiscordRoleAssignmentFailed,
                reward,
                preparation.PreviousCoins,
                roleResult.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord role reward purchase failed. RewardId: {RewardId}, UserId: {UserId}", reward.Id, profile.Id);

            var compensationSucceeded = await TryCompensatePurchaseAsync(
                profile.Id,
                preparation.PreviousCoins,
                userRewardId,
                rewardCodeId: null,
                reward.Type,
                cancellationToken);

            return BuildCompensatedFailureResult(
                compensationSucceeded,
                RewardPurchaseStatus.CompensationFailed,
                reward,
                preparation.PreviousCoins,
                ex.Message);
        }
    }

    private async Task<(bool Succeeded, int PreviousCoins, int RemainingCoins, int CurrentCoins)> TryPreparePurchaseAsync(
        Profile profile,
        Reward reward,
        CancellationToken cancellationToken)
    {
        if (profile.Coins < reward.PriceCoins)
        {
            return (false, profile.Coins, profile.Coins, profile.Coins);
        }

        var previousCoins = profile.Coins;
        var remainingCoins = previousCoins - reward.PriceCoins;
        var coinsUpdated = await TryUpdateProfileCoinsAsync(profile.Id, previousCoins, remainingCoins, cancellationToken);

        if (coinsUpdated)
        {
            return (true, previousCoins, remainingCoins, remainingCoins);
        }

        var refreshedProfile = await GetProfileAsync(profile.Id, cancellationToken);
        return (false, previousCoins, remainingCoins, refreshedProfile?.Coins ?? previousCoins);
    }

    private async Task<RewardCode?> TryReserveRewardCodeAsync(
        long rewardId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRewardCodeReservationAttempts; attempt += 1)
        {
            var rewardCodeResponse = await _supabase
                .From<RewardCode>()
                .Select("*")
                .Filter("reward_id", PostgrestOperator.Equals, rewardId.ToString())
                .Filter("is_used", PostgrestOperator.Equals, "false")
                .Order("id", PostgrestOrdering.Ascending)
                .Limit(1)
                .Get(cancellationToken);

            var rewardCode = rewardCodeResponse.Models.FirstOrDefault();
            if (rewardCode is null)
            {
                return null;
            }

            var updateResponse = await _supabase
                .From<RewardCode>()
                .Filter("id", PostgrestOperator.Equals, rewardCode.Id.ToString())
                .Filter("is_used", PostgrestOperator.Equals, "false")
                .Set(x => x.IsUsed, true)
                .Set(x => x.UsedByUserId!, userId)
                .Update(cancellationToken: cancellationToken);

            if (updateResponse.Models.Any())
            {
                rewardCode.IsUsed = true;
                rewardCode.UsedByUserId = userId;
                return rewardCode;
            }
        }

        return null;
    }

    private async Task<bool> TryUpdateProfileCoinsAsync(
        Guid userId,
        int expectedCoins,
        int newCoins,
        CancellationToken cancellationToken)
    {
        var updateResponse = await _supabase
            .From<Profile>()
            .Filter("id", PostgrestOperator.Equals, userId.ToString())
            .Filter("coins", PostgrestOperator.Equals, expectedCoins.ToString())
            .Set(x => x.Coins, newCoins)
            .Update(cancellationToken: cancellationToken);

        return updateResponse.Models.Any();
    }

    private async Task<long> InsertUserRewardAsync(
        Guid userId,
        long rewardId,
        CancellationToken cancellationToken)
    {
        var response = await _supabase
            .From<UserReward>()
            .Insert(new UserReward
            {
                UserId = userId,
                RewardId = rewardId
            }, new PostgrestQueryOptions(), cancellationToken);

        return response.Models.First().Id;
    }

    private async Task<bool> UserAlreadyOwnsRewardAsync(
        Guid userId,
        long rewardId,
        CancellationToken cancellationToken)
    {
        var response = await _supabase
            .From<UserReward>()
            .Select("*")
            .Filter("user_id", PostgrestOperator.Equals, userId.ToString())
            .Filter("reward_id", PostgrestOperator.Equals, rewardId.ToString())
            .Limit(1)
            .Get(cancellationToken);

        return response.Models.Any();
    }

    private async Task<bool> TryCompensatePurchaseAsync(
        Guid userId,
        int restoredCoins,
        long? userRewardId,
        long? rewardCodeId,
        string rewardType,
        CancellationToken cancellationToken)
    {
        try
        {
            await RestoreProfileCoinsAsync(userId, restoredCoins, cancellationToken);

            if (userRewardId.HasValue)
            {
                await DeleteUserRewardAsync(userRewardId.Value, cancellationToken);
            }

            if (rewardCodeId.HasValue)
            {
                await ReleaseRewardCodeAsync(rewardCodeId.Value, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to compensate reward purchase. RewardType: {RewardType}, UserId: {UserId}, UserRewardId: {UserRewardId}, RewardCodeId: {RewardCodeId}",
                rewardType,
                userId,
                userRewardId,
                rewardCodeId);
            return false;
        }
    }

    private async Task RestoreProfileCoinsAsync(
        Guid userId,
        int restoredCoins,
        CancellationToken cancellationToken)
    {
        await _supabase
            .From<Profile>()
            .Filter("id", PostgrestOperator.Equals, userId.ToString())
            .Set(x => x.Coins, restoredCoins)
            .Update(cancellationToken: cancellationToken);
    }

    private async Task ReleaseRewardCodeAsync(long rewardCodeId, CancellationToken cancellationToken)
    {
        await _supabase
            .From<RewardCode>()
            .Filter("id", PostgrestOperator.Equals, rewardCodeId.ToString())
            .Set(x => x.IsUsed, false)
            .Set(x => x.UsedByUserId!, null)
            .Update(cancellationToken: cancellationToken);
    }

    private async Task DeleteUserRewardAsync(long userRewardId, CancellationToken cancellationToken)
    {
        await _supabase
            .From<UserReward>()
            .Filter("id", PostgrestOperator.Equals, userRewardId.ToString())
            .Delete(cancellationToken: cancellationToken);
    }

    private static RewardPurchaseResult BuildPurchaseResult(
        RewardPurchaseStatus status,
        Reward reward,
        int? remainingCoins,
        string? failureDetail = null) =>
        new()
        {
            Status = status,
            RewardId = reward.Id,
            RewardName = reward.Name,
            RewardType = reward.Type,
            RemainingCoins = remainingCoins,
            FailureDetail = failureDetail
        };

    private static RewardPurchaseResult BuildCompensatedFailureResult(
        bool compensationSucceeded,
        RewardPurchaseStatus compensatedStatus,
        Reward reward,
        int restoredCoins,
        string? failureDetail) =>
        BuildPurchaseResult(
            compensationSucceeded ? compensatedStatus : RewardPurchaseStatus.CompensationFailed,
            reward,
            compensationSucceeded ? restoredCoins : null,
            failureDetail);

    private async Task<Profile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var response = await _supabase
            .From<Profile>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, userId.ToString())
            .Limit(1)
            .Get(cancellationToken);

        return response.Models.FirstOrDefault();
    }

    private async Task<Reward?> GetRewardAsync(long rewardId, CancellationToken cancellationToken)
    {
        var response = await _supabase
            .From<Reward>()
            .Select("*")
            .Filter("id", PostgrestOperator.Equals, rewardId.ToString())
            .Limit(1)
            .Get(cancellationToken);

        return response.Models.FirstOrDefault();
    }

    private static string BuildSubscriptionCodeMessage(string rewardName, string rewardCode) =>
        "Je reward is klaar!" + Environment.NewLine + Environment.NewLine +
        $"Reward: {rewardName}" + Environment.NewLine + Environment.NewLine +
        "Hier is je code:" + Environment.NewLine +
        rewardCode + Environment.NewLine + Environment.NewLine +
        "Deel je code niet met anderen.";
}

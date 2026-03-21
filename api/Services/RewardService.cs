using api.Models;
using Npgsql;
using PostgrestOperator = Supabase.Postgrest.Constants.Operator;
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
    DatabaseConfigurationMissing,
    DatabaseConnectionFailed,
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
    private readonly SupabaseClient _supabase;
    private readonly IDiscordService _discordService;
    private readonly ILogger<RewardService> _logger;
    private readonly string? _databaseConnectionString;

    public RewardService(
        SupabaseClient supabase,
        IDiscordService discordService,
        IConfiguration configuration,
        ILogger<RewardService> logger)
    {
        _supabase = supabase;
        _discordService = discordService;
        _logger = logger;
        _databaseConnectionString = configuration["Supabase:DatabaseConnectionString"];
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
        if (string.IsNullOrWhiteSpace(_databaseConnectionString))
        {
            _logger.LogError("Reward purchase requires Supabase:DatabaseConnectionString or SUPABASE_DB_CONNECTION_STRING.");
            return new RewardPurchaseResult
            {
                Status = RewardPurchaseStatus.DatabaseConfigurationMissing,
                RewardId = rewardId,
                FailureDetail = "Configureer SUPABASE_DB_CONNECTION_STRING, DATABASE_URL of een Azure App Service connection string met naam 'SupabaseDb'."
            };
        }

        try
        {
            await using var connection = new NpgsqlConnection(_databaseConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var profile = await GetProfileForUpdateAsync(connection, transaction, userId, cancellationToken);
            if (profile is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RewardPurchaseResult
                {
                    Status = RewardPurchaseStatus.ProfileNotFound,
                    RewardId = rewardId
                };
            }

            var reward = await GetRewardRowAsync(connection, transaction, rewardId, cancellationToken);
            if (reward is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RewardPurchaseResult
                {
                    Status = RewardPurchaseStatus.RewardNotFound,
                    RewardId = rewardId
                };
            }

            if (!reward.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BuildFailedPurchaseResult(
                    RewardPurchaseStatus.RewardInactive,
                    reward,
                    profile.Coins);
            }

            if (string.IsNullOrWhiteSpace(profile.DiscordId))
            {
                await transaction.RollbackAsync(cancellationToken);
                return BuildFailedPurchaseResult(
                    RewardPurchaseStatus.DiscordAccountRequired,
                    reward,
                    profile.Coins);
            }

            if (profile.Coins < reward.PriceCoins)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BuildFailedPurchaseResult(
                    RewardPurchaseStatus.InsufficientCoins,
                    reward,
                    profile.Coins);
            }

            return reward.Type switch
            {
                RewardTypes.SubscriptionCode => await PurchaseSubscriptionCodeAsync(
                    connection,
                    transaction,
                    profile,
                    reward,
                    cancellationToken),
                RewardTypes.DiscordRole => await PurchaseDiscordRoleAsync(
                    connection,
                    transaction,
                    profile,
                    reward,
                    cancellationToken),
                _ => await RollbackUnsupportedRewardAsync(transaction, reward, profile.Coins, cancellationToken)
            };
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or ArgumentException)
        {
            _logger.LogError(ex, "Rewards database connection failed for reward {RewardId}.", rewardId);
            return new RewardPurchaseResult
            {
                Status = RewardPurchaseStatus.DatabaseConnectionFailed,
                RewardId = rewardId,
                FailureDetail = ex.Message
            };
        }
    }

    private static int GetTypeSortOrder(Reward reward) => reward.Type switch
    {
        RewardTypes.SubscriptionCode => 0,
        RewardTypes.DiscordRole => 1,
        _ => 2
    };

    private async Task<RewardPurchaseResult> PurchaseSubscriptionCodeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProfileSnapshot profile,
        RewardSnapshot reward,
        CancellationToken cancellationToken)
    {
        var availableCode = await GetAvailableRewardCodeForUpdateAsync(
            connection,
            transaction,
            reward.Id,
            cancellationToken);

        if (availableCode is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BuildFailedPurchaseResult(
                RewardPurchaseStatus.NoCodesAvailable,
                reward,
                profile.Coins);
        }

        var remainingCoins = profile.Coins - reward.PriceCoins;
        await UpdateProfileCoinsAsync(connection, transaction, profile.Id, remainingCoins, cancellationToken);
        var userRewardId = await InsertUserRewardAsync(connection, transaction, profile.Id, reward.Id, cancellationToken);
        await MarkRewardCodeAsUsedAsync(connection, transaction, availableCode.Id, profile.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var dmMessage = BuildSubscriptionCodeMessage(reward.Name, availableCode.CodeValue);
        var dmResult = await _discordService.SendDirectMessageAsync(profile.DiscordId!, dmMessage, cancellationToken);
        if (dmResult.Success)
        {
            return new RewardPurchaseResult
            {
                Status = RewardPurchaseStatus.Success,
                RewardId = reward.Id,
                RewardName = reward.Name,
                RewardType = reward.Type,
                RemainingCoins = remainingCoins
            };
        }

        _logger.LogWarning(
            "Reward DM failed after purchase commit. RewardId: {RewardId}, UserId: {UserId}, Detail: {Detail}",
            reward.Id,
            profile.Id,
            dmResult.Message);

        var compensationSucceeded = await RevertSubscriptionPurchaseAsync(
            profile.Id,
            reward.PriceCoins,
            userRewardId,
            availableCode.Id,
            cancellationToken);

        return new RewardPurchaseResult
        {
            Status = compensationSucceeded
                ? RewardPurchaseStatus.DiscordDmFailed
                : RewardPurchaseStatus.CompensationFailed,
            RewardId = reward.Id,
            RewardName = reward.Name,
            RewardType = reward.Type,
            RemainingCoins = compensationSucceeded ? profile.Coins : null,
            FailureDetail = dmResult.Message
        };
    }

    private async Task<RewardPurchaseResult> PurchaseDiscordRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProfileSnapshot profile,
        RewardSnapshot reward,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reward.DiscordRoleId))
        {
            await transaction.RollbackAsync(cancellationToken);
            return BuildFailedPurchaseResult(
                RewardPurchaseStatus.DiscordRoleNotConfigured,
                reward,
                profile.Coins);
        }

        var alreadyPurchased = await UserAlreadyOwnsRewardAsync(
            connection,
            transaction,
            profile.Id,
            reward.Id,
            cancellationToken);

        if (alreadyPurchased)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BuildFailedPurchaseResult(
                RewardPurchaseStatus.AlreadyPurchased,
                reward,
                profile.Coins);
        }

        var remainingCoins = profile.Coins - reward.PriceCoins;
        await UpdateProfileCoinsAsync(connection, transaction, profile.Id, remainingCoins, cancellationToken);
        var userRewardId = await InsertUserRewardAsync(connection, transaction, profile.Id, reward.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var roleResult = await _discordService.AddRoleToUserAsync(
            profile.DiscordId!,
            reward.DiscordRoleId,
            cancellationToken);

        if (roleResult.Success)
        {
            return new RewardPurchaseResult
            {
                Status = RewardPurchaseStatus.Success,
                RewardId = reward.Id,
                RewardName = reward.Name,
                RewardType = reward.Type,
                RemainingCoins = remainingCoins
            };
        }

        _logger.LogWarning(
            "Discord role assignment failed after purchase commit. RewardId: {RewardId}, UserId: {UserId}, Detail: {Detail}",
            reward.Id,
            profile.Id,
            roleResult.Message);

        var compensationSucceeded = await RevertDiscordRolePurchaseAsync(
            profile.Id,
            reward.PriceCoins,
            userRewardId,
            cancellationToken);

        return new RewardPurchaseResult
        {
            Status = compensationSucceeded
                ? RewardPurchaseStatus.DiscordRoleAssignmentFailed
                : RewardPurchaseStatus.CompensationFailed,
            RewardId = reward.Id,
            RewardName = reward.Name,
            RewardType = reward.Type,
            RemainingCoins = compensationSucceeded ? profile.Coins : null,
            FailureDetail = roleResult.Message
        };
    }

    private async Task<RewardPurchaseResult> RollbackUnsupportedRewardAsync(
        NpgsqlTransaction transaction,
        RewardSnapshot reward,
        int currentCoins,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return BuildFailedPurchaseResult(
            RewardPurchaseStatus.UnsupportedRewardType,
            reward,
            currentCoins);
    }

    private async Task<bool> RevertSubscriptionPurchaseAsync(
        Guid userId,
        int refundedCoins,
        long userRewardId,
        long rewardCodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_databaseConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await ExecuteAsync(
                connection,
                transaction,
                """
                update profiles
                set coins = coins + @coins
                where id = @userId;
                """,
                new NpgsqlParameter("coins", refundedCoins),
                new NpgsqlParameter("userId", userId),
                cancellationToken);

            await ExecuteAsync(
                connection,
                transaction,
                """
                delete from user_rewards
                where id = @userRewardId;
                """,
                new NpgsqlParameter("userRewardId", userRewardId),
                cancellationToken);

            await ExecuteAsync(
                connection,
                transaction,
                """
                update reward_codes
                set is_used = false,
                    used_by_user_id = null
                where id = @rewardCodeId;
                """,
                new NpgsqlParameter("rewardCodeId", rewardCodeId),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to compensate subscription reward purchase. UserId: {UserId}, UserRewardId: {UserRewardId}, RewardCodeId: {RewardCodeId}",
                userId,
                userRewardId,
                rewardCodeId);
            return false;
        }
    }

    private async Task<bool> RevertDiscordRolePurchaseAsync(
        Guid userId,
        int refundedCoins,
        long userRewardId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_databaseConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await ExecuteAsync(
                connection,
                transaction,
                """
                update profiles
                set coins = coins + @coins
                where id = @userId;
                """,
                new NpgsqlParameter("coins", refundedCoins),
                new NpgsqlParameter("userId", userId),
                cancellationToken);

            await ExecuteAsync(
                connection,
                transaction,
                """
                delete from user_rewards
                where id = @userRewardId;
                """,
                new NpgsqlParameter("userRewardId", userRewardId),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to compensate Discord role reward purchase. UserId: {UserId}, UserRewardId: {UserRewardId}",
                userId,
                userRewardId);
            return false;
        }
    }

    private static RewardPurchaseResult BuildFailedPurchaseResult(
        RewardPurchaseStatus status,
        RewardSnapshot reward,
        int currentCoins) =>
        new()
        {
            Status = status,
            RewardId = reward.Id,
            RewardName = reward.Name,
            RewardType = reward.Type,
            RemainingCoins = currentCoins
        };

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

    private static async Task<ProfileSnapshot?> GetProfileForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select id, coins, discord_id
            from profiles
            where id = @userId
            for update;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProfileSnapshot(
            reader.GetGuid(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task<RewardSnapshot?> GetRewardRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long rewardId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select id, name, type, price_coins, is_active, discord_role_id
            from rewards
            where id = @rewardId
            limit 1;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("rewardId", rewardId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RewardSnapshot(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetBoolean(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    private static async Task<RewardCodeSnapshot?> GetAvailableRewardCodeForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long rewardId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select id, code_value
            from reward_codes
            where reward_id = @rewardId
              and is_used = false
            order by id
            limit 1
            for update skip locked;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("rewardId", rewardId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RewardCodeSnapshot(
            reader.GetInt64(0),
            reader.GetString(1));
    }

    private static async Task<bool> UserAlreadyOwnsRewardAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        long rewardId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            select 1
            from user_rewards
            where user_id = @userId
              and reward_id = @rewardId
            limit 1;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("rewardId", rewardId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private static async Task UpdateProfileCoinsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        int remainingCoins,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            update profiles
            set coins = @remainingCoins
            where id = @userId;
            """,
            new NpgsqlParameter("remainingCoins", remainingCoins),
            new NpgsqlParameter("userId", userId),
            cancellationToken);
    }

    private static async Task<long> InsertUserRewardAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        long rewardId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into user_rewards (user_id, reward_id)
            values (@userId, @rewardId)
            returning id;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("rewardId", rewardId);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar);
    }

    private static async Task MarkRewardCodeAsUsedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long rewardCodeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            update reward_codes
            set is_used = true,
                used_by_user_id = @userId
            where id = @rewardCodeId;
            """,
            new NpgsqlParameter("userId", userId),
            new NpgsqlParameter("rewardCodeId", rewardCodeId),
            cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        NpgsqlParameter parameter,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        NpgsqlParameter firstParameter,
        NpgsqlParameter secondParameter,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add(firstParameter);
        command.Parameters.Add(secondParameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildSubscriptionCodeMessage(string rewardName, string rewardCode) =>
        "Je reward is klaar!" + Environment.NewLine + Environment.NewLine +
        $"Reward: {rewardName}" + Environment.NewLine + Environment.NewLine +
        "Hier is je code:" + Environment.NewLine +
        rewardCode + Environment.NewLine + Environment.NewLine +
        "Deel je code niet met anderen.";

    private sealed record ProfileSnapshot(Guid Id, int Coins, string? DiscordId);
    private sealed record RewardSnapshot(long Id, string Name, string Type, int PriceCoins, bool IsActive, string? DiscordRoleId);
    private sealed record RewardCodeSnapshot(long Id, string CodeValue);
}

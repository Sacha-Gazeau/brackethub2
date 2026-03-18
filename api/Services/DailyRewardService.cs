using Supabase;
using api.Models;

namespace api.Services;

public enum ClaimDailyStatus
{
    Success,
    NotFound,
    AlreadyClaimed
}

public sealed class ClaimDailyResult
{
    public ClaimDailyStatus Status { get; init; }
    public int? Coins { get; init; }
    public DateTime? LastDailyClaim { get; init; }
    public bool AlreadyClaimed => Status == ClaimDailyStatus.AlreadyClaimed;
}

public class DailyRewardService
{
    private const int DailyRewardAmount = 50;
    private readonly Client _supabase;

    public DailyRewardService(Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<ClaimDailyResult> ClaimDailyAsync(Guid userId)
    {
        var profileResponse = await _supabase
            .From<Profile>()
            .Where(x => x.Id == userId)
            .Get();

        var profile = profileResponse.Models.FirstOrDefault();
        if (profile == null)
        {
            return new ClaimDailyResult { Status = ClaimDailyStatus.NotFound };
        }

        var todayUtc = DateTime.UtcNow.Date;
        if (profile.LastDailyClaim.HasValue && profile.LastDailyClaim.Value.Date == todayUtc)
        {
            return new ClaimDailyResult
            {
                Status = ClaimDailyStatus.AlreadyClaimed,
                Coins = profile.Coins,
                LastDailyClaim = profile.LastDailyClaim
            };
        }

        profile.Coins += DailyRewardAmount;
        profile.LifetimeCoins = (profile.LifetimeCoins ?? 0) + DailyRewardAmount;
        profile.LastDailyClaim = DateTime.UtcNow;

        await profile.Update<Profile>();

        return new ClaimDailyResult
        {
            Status = ClaimDailyStatus.Success,
            Coins = profile.Coins,
            LastDailyClaim = profile.LastDailyClaim
        };
    }
}

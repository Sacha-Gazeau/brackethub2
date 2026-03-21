using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("user_rewards")]
public class UserReward : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("reward_id")]
    public long RewardId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

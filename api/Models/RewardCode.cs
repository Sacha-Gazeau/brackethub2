using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("reward_codes")]
public class RewardCode : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("reward_id")]
    public long RewardId { get; set; }

    [Column("code_value")]
    public string CodeValue { get; set; } = string.Empty;

    [Column("is_used")]
    public bool IsUsed { get; set; }

    [Column("used_by_user_id")]
    public Guid? UsedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

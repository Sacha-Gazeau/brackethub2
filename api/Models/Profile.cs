using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("profiles")]
public class Profile : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("username")]
    public string? Username { get; set; }

    [Column("avatar")]
    public string? Avatar { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("discord_id")]
    public string? DiscordId { get; set; }

    [Column("is_in_server")]
    public bool? IsInServer { get; set; }

    [Column("coins")]
    public int Coins { get; set; }

    [Column("lifetimecoins")]
    public int? LifetimeCoins { get; set; }

    [Column("last_daily_claim")]
    public DateTime? LastDailyClaim { get; set; }
}

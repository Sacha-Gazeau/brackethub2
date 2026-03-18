using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("bets")]
public class Bet : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("tournament_id")]
    public long TournamentId { get; set; }

    [Column("team_id")]
    public long TeamId { get; set; }

    [Column("coins_bet")]
    public int CoinsBet { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("paid_out")]
    public bool PaidOut { get; set; }

    [Column("paid_out_at")]
    public DateTime? PaidOutAt { get; set; }
}

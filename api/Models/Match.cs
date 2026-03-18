using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("matches")]
public class Match : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("tournament_id")]
    public long TournamentId { get; set; }

    [Column("round")]
    public long Round { get; set; }

    [Column("match_number")]
    public long MatchNumber { get; set; }

    [Column("team1_id")]
    public long? Team1Id { get; set; }

    [Column("team2_id")]
    public long? Team2Id { get; set; }

    [Column("winner_id")]
    public long? WinnerId { get; set; }

    [Column("team1_score")]
    public long Team1Score { get; set; }

    [Column("team2_score")]
    public long Team2Score { get; set; }

    [Column("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }
}

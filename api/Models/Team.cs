using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("teams")]
public class Team : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("tournament_id")]
    public long TournamentId { get; set; }

    [Column("captain_id")]
    public Guid CaptainId { get; set; }

    [Column("status")]
    public TeamStatus Status { get; set; }

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

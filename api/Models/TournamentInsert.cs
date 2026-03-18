using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("tournaments")]
public class TournamentInsert : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Column("game_igdb_id")]
    public long? GameIgdbId { get; set; }

    [Column("format")]
    public int Format { get; set; }

    [Column("max_teams")]
    public int MaxTeams { get; set; }

    [Column("min_teams")]
    public int MinTeams { get; set; }

    [Column("current_teams")]
    public int CurrentTeams { get; set; }

    [Column("players_per_team")]
    public int PlayersPerTeam { get; set; }

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("privacy")]
    public string Privacy { get; set; } = "public";

    [Column("team_status")]
    public TournamentTeamStatus TeamStatus { get; set; }

    [Column("final_format")]
    public int? FinalFormat { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("tournament_type")]
    public string TournamentType { get; set; } = "pending";

    [Column("winner_team_id")]
    public long? WinnerTeamId { get; set; }
}

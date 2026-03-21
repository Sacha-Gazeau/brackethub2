using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("groups")]
public class TournamentGroup : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("tournament_id")]
    public long TournamentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("position")]
    public int? Position { get; set; }
}

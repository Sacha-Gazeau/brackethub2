using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("group_teams")]
public class GroupTeam : BaseModel
{
    [PrimaryKey("group_id", false)]
    public long GroupId { get; set; }

    [Column("team_id")]
    public long TeamId { get; set; }

    [Column("seed")]
    public int? Seed { get; set; }
}

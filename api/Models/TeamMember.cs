using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("team_members")]
public class TeamMember : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("team_id")]
    public long TeamId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

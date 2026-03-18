using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("formats")]
public class Format : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

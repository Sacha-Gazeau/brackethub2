using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace api.Models;

[Table("notification_deliveries")]
public class NotificationDelivery : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("notification_type")]
    public string NotificationType { get; set; } = string.Empty;

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("reference_key")]
    public string ReferenceKey { get; set; } = string.Empty;

    [Column("success")]
    public bool Success { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

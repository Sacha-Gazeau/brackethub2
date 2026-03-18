using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace api.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum TeamStatus
{
    [EnumMember(Value = "pending")]
    Pending,

    [EnumMember(Value = "accepted")]
    Accepted,

    [EnumMember(Value = "rejected")]
    Rejected
}

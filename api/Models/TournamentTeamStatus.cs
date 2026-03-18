using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace api.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum TournamentTeamStatus
{
    [EnumMember(Value = "open")]
    Open,

    [EnumMember(Value = "ready")]
    Ready,

    [EnumMember(Value = "full")]
    Full
}

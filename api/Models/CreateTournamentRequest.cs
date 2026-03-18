using System.Text.Json.Serialization;

namespace api.Models;

public class CreateTournamentRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("game_igdb_id")]
    public long GameIgdbId { get; set; }

    [JsonPropertyName("format")]
    public int Format { get; set; }

    [JsonPropertyName("max_teams")]
    public int MaxTeams { get; set; }

    [JsonPropertyName("min_teams")]
    public int MinTeams { get; set; }

    [JsonPropertyName("players_per_team")]
    public int PlayersPerTeam { get; set; }

    [JsonPropertyName("start_date")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("end_date")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("privacy")]
    public string Privacy { get; set; } = "public";

    [JsonPropertyName("final_format")]
    public int? FinalFormat { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

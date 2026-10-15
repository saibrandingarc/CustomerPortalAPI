using System.Text.Json.Serialization;

namespace CustomerPortalAPI.Models;

public class GoogleSheetRow
{
    [JsonPropertyName("Project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("Main Content Type")]
    public string MainContentType { get; set; } = string.Empty;

    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("Credits")]
    public string Credits { get; set; } = string.Empty;

    [JsonPropertyName("Topic Category")]
    public string TopicCategory { get; set; } = string.Empty;
}

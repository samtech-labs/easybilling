using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class AnafNotFound
{
    [JsonPropertyName("cui")]
    public long Cui { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

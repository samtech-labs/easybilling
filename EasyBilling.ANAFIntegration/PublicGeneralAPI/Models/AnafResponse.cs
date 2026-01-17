using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class AnafResponse
{
    [JsonPropertyName("cod")]
    public int Cod { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("found")]
    public List<AnafCompanyResult>? Found { get; set; }

    [JsonPropertyName("notFound")]
    public List<AnafNotFound>? NotFound { get; set; }
}

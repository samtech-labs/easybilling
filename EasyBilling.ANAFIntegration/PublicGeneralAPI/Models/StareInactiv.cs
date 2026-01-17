using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class StareInactiv
{
    [JsonPropertyName("dataInactivare")]
    public string? DataInactivare { get; set; }

    [JsonPropertyName("dataReactivare")]
    public string? DataReactivare { get; set; }

    [JsonPropertyName("dataPublicare")]
    public string? DataPublicare { get; set; }

    [JsonPropertyName("dataRadiere")]
    public string? DataRadiere { get; set; }

    [JsonPropertyName("statusInactivi")]
    public bool StatusInactivi { get; set; }
}

using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class InregistrareSplitTVA
{
    [JsonPropertyName("dataInceputSplitTVA")]
    public string? DataInceputSplitTVA { get; set; }

    [JsonPropertyName("dataAnulareSplitTVA")]
    public string? DataAnulareSplitTVA { get; set; }

    [JsonPropertyName("statusSplitTVA")]
    public bool StatusSplitTVA { get; set; }
}

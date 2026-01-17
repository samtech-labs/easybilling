using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class InregistrareRTVAI
{
    [JsonPropertyName("dataInceputTvaInc")]
    public string? DataInceputTvaInc { get; set; }

    [JsonPropertyName("dataSfarsitTvaInc")]
    public string? DataSfarsitTvaInc { get; set; }

    [JsonPropertyName("dataActualizareTvaInc")]
    public string? DataActualizareTvaInc { get; set; }

    [JsonPropertyName("dataPublicareTvaInc")]
    public string? DataPublicareTvaInc { get; set; }

    [JsonPropertyName("tipActTvaInc")]
    public string? TipActTvaInc { get; set; }

    [JsonPropertyName("statusTvaIncasare")]
    public bool StatusTvaIncasare { get; set; }
}

using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class InregistrareScopTva
{
    [JsonPropertyName("scpTVA")]
    public bool ScpTVA { get; set; }

    [JsonPropertyName("data_inceput_ScpTVA")]
    public string? DataInceputScpTVA { get; set; }

    [JsonPropertyName("data_sfarsit_ScpTVA")]
    public string? DataSfarsitScpTVA { get; set; }

    [JsonPropertyName("data_anul_imp_ScpTVA")]
    public string? DataAnulImpScpTVA { get; set; }

    [JsonPropertyName("mesaj_ScpTVA")]
    public string? MesajScpTVA { get; set; }
}
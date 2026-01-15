using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class AdresaDomiciliuFiscal
{
    [JsonPropertyName("ddenumire_Strada")]
    public string? DdenumireStrada { get; set; }

    [JsonPropertyName("dnumar_Strada")]
    public string? DnumarStrada { get; set; }

    [JsonPropertyName("ddenumire_Localitate")]
    public string? DdenumireLocalitate { get; set; }

    [JsonPropertyName("dcod_Localitate")]
    public string? DcodLocalitate { get; set; }

    [JsonPropertyName("ddenumire_Judet")]
    public string? DdenumireJudet { get; set; }

    [JsonPropertyName("dcod_Judet")]
    public string? DcodJudet { get; set; }

    [JsonPropertyName("dcod_JudetAuto")]
    public string? DcodJudetAuto { get; set; }

    [JsonPropertyName("dtara")]
    public string? Dtara { get; set; }

    [JsonPropertyName("ddetalii_Adresa")]
    public string? DdetaliiAdresa { get; set; }

    [JsonPropertyName("dcod_Postal")]
    public string? DcodPostal { get; set; }
}
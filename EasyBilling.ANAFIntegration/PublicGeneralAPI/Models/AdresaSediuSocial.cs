using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class AdresaSediuSocial
{
    [JsonPropertyName("sdenumire_Strada")]
    public string? SdenumireStrada { get; set; }

    [JsonPropertyName("snumar_Strada")]
    public string? SnumarStrada { get; set; }

    [JsonPropertyName("sdenumire_Localitate")]
    public string? SdenumireLocalitate { get; set; }

    [JsonPropertyName("scod_Localitate")]
    public string? ScodLocalitate { get; set; }

    [JsonPropertyName("sdenumire_Judet")]
    public string? SdenumireJudet { get; set; }

    [JsonPropertyName("scod_Judet")]
    public string? ScodJudet { get; set; }

    [JsonPropertyName("scod_JudetAuto")]
    public string? ScodJudetAuto { get; set; }

    [JsonPropertyName("stara")]
    public string? Stara { get; set; }

    [JsonPropertyName("sdetalii_Adresa")]
    public string? SdetaliiAdresa { get; set; }

    [JsonPropertyName("scod_Postal")]
    public string? ScodPostal { get; set; }
}

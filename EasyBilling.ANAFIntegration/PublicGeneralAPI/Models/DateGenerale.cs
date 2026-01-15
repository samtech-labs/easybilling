using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class DateGenerale
{
    [JsonPropertyName("cui")]
    public long? Cui { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("denumire")]
    public string? Denumire { get; set; }

    [JsonPropertyName("adresa")]
    public string? Adresa { get; set; }

    [JsonPropertyName("nrRegCom")]
    public string? NrRegCom { get; set; }

    [JsonPropertyName("telefon")]
    public string? Telefon { get; set; }

    [JsonPropertyName("fax")]
    public string? Fax { get; set; }

    [JsonPropertyName("codPostal")]
    public string? CodPostal { get; set; }

    [JsonPropertyName("act")]
    public string? Act { get; set; }

    [JsonPropertyName("stare_inregistrare")]
    public string? StareInregistrare { get; set; }

    [JsonPropertyName("data_inregistrare")]
    public string? DataInregistrare { get; set; }

    [JsonPropertyName("cod_CAEN")]
    public string? CodCAEN { get; set; }

    [JsonPropertyName("iban")]
    public string? Iban { get; set; }

    [JsonPropertyName("statusRO_e_Factura")]
    public bool StatusROEFactura { get; set; }

    [JsonPropertyName("organFiscalCompetent")]
    public string? OrganFiscalCompetent { get; set; }

    [JsonPropertyName("forma_de_proprietate")]
    public string? FormaDeProprietate { get; set; }

    [JsonPropertyName("forma_organizare")]
    public string? FormaOrganizare { get; set; }

    [JsonPropertyName("forma_juridica")]
    public string? FormaJuridica { get; set; }
}

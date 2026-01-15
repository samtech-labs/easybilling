using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

internal class AnafCompanyResult
{
    [JsonPropertyName("date_generale")]
    public DateGenerale? DateGenerale { get; set; }

    [JsonPropertyName("inregistrare_scop_Tva")]
    public InregistrareScopTva? InregistrareScopTva { get; set; }

    [JsonPropertyName("inregistrare_RTVAI")]
    public InregistrareRTVAI? InregistrareRTVAI { get; set; }

    [JsonPropertyName("stare_inactiv")]
    public StareInactiv? StareInactiv { get; set; }

    [JsonPropertyName("inregistrare_SplitTVA")]
    public InregistrareSplitTVA? InregistrareSplitTVA { get; set; }

    [JsonPropertyName("adresa_sediu_social")]
    public AdresaSediuSocial? AdresaSediuSocial { get; set; }

    [JsonPropertyName("adresa_domiciliu_fiscal")]
    public AdresaDomiciliuFiscal? AdresaDomiciliuFiscal { get; set; }
}

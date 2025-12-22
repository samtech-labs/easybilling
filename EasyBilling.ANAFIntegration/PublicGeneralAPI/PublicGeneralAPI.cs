using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyBilling.ANAFIntegration.Models;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI
{
    public class PublicGeneralAPI
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://webservicesp.anaf.ro/api/PlatitorTvaRest/v9/tva";

        public PublicGeneralAPI()
        {
            _httpClient = new HttpClient();
        }

        public PublicGeneralAPI(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CompanyDetails?> GetCompanyDetailsAsync(string cui, DateTime date)
        {
            var cleanCui = cui.Replace("RO", "").Replace(" ", "").Trim();

            if (!long.TryParse(cleanCui, out var cuiNumber))
                throw new ArgumentException("Invalid CUI format", nameof(cui));

            var requestBody = new[]
            {
                new
                {
                    cui = cuiNumber,
                    data = date.ToString("yyyy-MM-dd")
                }
            };

            var response = await _httpClient.PostAsJsonAsync(BaseUrl, requestBody);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var anafResponse = JsonSerializer.Deserialize<AnafResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var found = anafResponse?.Found?.FirstOrDefault();
            if (found?.DateGenerale == null)
                return null;

            return MapToCompanyDetails(found);
        }

        private static CompanyDetails MapToCompanyDetails(AnafCompanyResult result)
        {
            var general = result.DateGenerale!;
            var vat = result.InregistrareScopTva;
            var cashVat = result.InregistrareRTVAI;
            var inactive = result.StareInactiv;
            var splitVat = result.InregistrareSplitTVA;
            var regAddress = result.AdresaSediuSocial;
            var taxAddress = result.AdresaDomiciliuFiscal;

            return new CompanyDetails
            {
                // General Info
                CUI = general.Cui?.ToString(),
                QueryDate = ParseDate(general.Data),
                Name = general.Denumire,
                Address = general.Adresa,
                RegistrationNumber = general.NrRegCom,
                Phone = general.Telefon,
                Fax = general.Fax,
                PostalCode = general.CodPostal,
                LegalDocument = general.Act,
                RegistrationStatus = general.StareInregistrare,
                RegistrationDate = ParseDate(general.DataInregistrare),
                CAENCode = general.CodCAEN,
                IBAN = general.Iban,
                IsEFacturaActive = general.StatusROEFactura,
                TaxAuthority = general.OrganFiscalCompetent,
                OwnershipForm = general.FormaDeProprietate,
                OrganizationForm = general.FormaOrganizare,
                LegalForm = general.FormaJuridica,

                // VAT Registration
                IsVatPayer = vat?.ScpTVA ?? false,
                VatStartDate = ParseDate(vat?.DataInceputScpTVA),
                VatEndDate = ParseDate(vat?.DataSfarsitScpTVA),
                VatTaxYearDate = ParseDate(vat?.DataAnulImpScpTVA),
                VatMessage = vat?.MesajScpTVA,

                // Cash-basis VAT
                CashVatStartDate = ParseDate(cashVat?.DataInceputTvaInc),
                CashVatEndDate = ParseDate(cashVat?.DataSfarsitTvaInc),
                CashVatUpdateDate = ParseDate(cashVat?.DataActualizareTvaInc),
                CashVatPublishDate = ParseDate(cashVat?.DataPublicareTvaInc),
                CashVatDocumentType = cashVat?.TipActTvaInc,
                IsCashVatPayer = cashVat?.StatusTvaIncasare ?? false,

                // Inactive Status
                InactivationDate = ParseDate(inactive?.DataInactivare),
                ReactivationDate = ParseDate(inactive?.DataReactivare),
                InactivePublishDate = ParseDate(inactive?.DataPublicare),
                DeregistrationDate = ParseDate(inactive?.DataRadiere),
                IsInactive = inactive?.StatusInactivi ?? false,

                // Split VAT
                SplitVatStartDate = ParseDate(splitVat?.DataInceputSplitTVA),
                SplitVatCancelDate = ParseDate(splitVat?.DataAnulareSplitTVA),
                IsSplitVatPayer = splitVat?.StatusSplitTVA ?? false,

                // Addresses
                RegisteredAddress = MapAddress(regAddress),
                TaxDomicileAddress = MapTaxAddress(taxAddress)
            };
        }

        private static AddressDetails? MapAddress(AdresaSediuSocial? addr)
        {
            if (addr == null) return null;

            return new AddressDetails
            {
                Street = addr.SdenumireStrada,
                StreetNumber = addr.SnumarStrada,
                City = addr.SdenumireLocalitate,
                CityCode = addr.ScodLocalitate,
                County = addr.SdenumireJudet,
                CountyCode = addr.ScodJudet,
                CountyAutoCode = addr.ScodJudetAuto,
                Country = addr.Stara,
                Details = addr.SdetaliiAdresa,
                PostalCode = addr.ScodPostal
            };
        }

        private static AddressDetails? MapTaxAddress(AdresaDomiciliuFiscal? addr)
        {
            if (addr == null) return null;

            return new AddressDetails
            {
                Street = addr.DdenumireStrada,
                StreetNumber = addr.DnumarStrada,
                City = addr.DdenumireLocalitate,
                CityCode = addr.DcodLocalitate,
                County = addr.DdenumireJudet,
                CountyCode = addr.DcodJudet,
                CountyAutoCode = addr.DcodJudetAuto,
                Country = addr.Dtara,
                Details = addr.DdetaliiAdresa,
                PostalCode = addr.DcodPostal
            };
        }

        private static DateTime? ParseDate(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return null;
            return DateTime.TryParse(dateStr, out var date) ? date : null;
        }
    }

    #region ANAF Response Models

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

    internal class AnafNotFound
    {
        [JsonPropertyName("cui")]
        public long Cui { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

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

    internal class InregistrareSplitTVA
    {
        [JsonPropertyName("dataInceputSplitTVA")]
        public string? DataInceputSplitTVA { get; set; }

        [JsonPropertyName("dataAnulareSplitTVA")]
        public string? DataAnulareSplitTVA { get; set; }

        [JsonPropertyName("statusSplitTVA")]
        public bool StatusSplitTVA { get; set; }
    }

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

    #endregion
}

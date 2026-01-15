using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

namespace EasyBilling.ANAFIntegration.PublicGeneralAPI;

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


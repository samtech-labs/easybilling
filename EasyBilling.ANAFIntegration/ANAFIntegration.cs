using EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EasyBilling.ANAFIntegration;

public static class ANAFIntegration
{
    private static readonly PublicGeneralAPI.PublicGeneralAPI _publicGeneralAPI = new(new HttpClient());
    private static readonly EFactura.EFactura _eFactura = new();
    private static ILogger _logger = NullLoggerFactory.Instance.CreateLogger("ANAFIntegration");

    public static PublicGeneralAPI.PublicGeneralAPI PublicGeneralAPI => _publicGeneralAPI;
    public static EFactura.EFactura EFactura => _eFactura;

    public static void SetLogger(ILogger logger)
    {
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger("ANAFIntegration");
        _logger.LogDebug("ANAFIntegration logger initialized");
    }

    public static async Task<CompanyDetails?> GetCompanyDetails(string cui, DateTime date)
    {
        _logger.LogInformation("Retrieving company details from ANAF for CUI: {CUI}, Date: {Date}",
            cui, date.ToString("yyyy-MM-dd"));

        try
        {
            if (string.IsNullOrWhiteSpace(cui))
            {
                _logger.LogWarning("GetCompanyDetails called with empty CUI");
                return null;
            }

            var cleanCui = cui.Replace("RO", "").Replace(" ", "").Trim();
            _logger.LogDebug("Cleaned CUI: {CleanCUI} (original: {OriginalCUI})", cleanCui, cui);

            var companyDetails = await _publicGeneralAPI.GetCompanyDetailsAsync(cleanCui, date);

            if (companyDetails == null)
            {
                _logger.LogWarning("No company details found in ANAF for CUI: {CUI}", cleanCui);
                return null;
            }

            _logger.LogInformation("Company details successfully retrieved from ANAF - Name: {CompanyName}, CUI: {CUI}, IsVatPayer: {IsVatPayer}",
                companyDetails.Name, cleanCui, companyDetails.IsVatPayer);

            return companyDetails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company details from ANAF for CUI: {CUI}", cui);
            throw;
        }
    }
}
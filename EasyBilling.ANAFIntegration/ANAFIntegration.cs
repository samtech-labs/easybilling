using EasyBilling.ANAFIntegration.Models;

namespace EasyBilling.ANAFIntegration
{
    public static class ANAFIntegration
    {
        private static readonly PublicGeneralAPI.PublicGeneralAPI _publicGeneralAPI = new();
        private static readonly EFactura.EFactura _eFactura = new();

        public static PublicGeneralAPI.PublicGeneralAPI PublicGeneralAPI => _publicGeneralAPI;
        public static EFactura.EFactura EFactura => _eFactura;

        public static async Task<CompanyDetails?> GetCompanyDetails(string cui, DateTime date)
        {
            return await _publicGeneralAPI.GetCompanyDetailsAsync(cui, date);
        }
    }
}

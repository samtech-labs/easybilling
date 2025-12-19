namespace EasyBilling.ANAFIntegration.EFactura
{
    public class EFactura
    {
        private readonly HttpClient _httpClient;

        public EFactura()
        {
            _httpClient = new HttpClient();
        }

        public EFactura(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Future e-factura functions will be added here
        // Examples:
        // - UploadInvoiceAsync
        // - GetInvoiceStatusAsync
        // - DownloadInvoiceAsync
        // - GetMessagesAsync
    }
}

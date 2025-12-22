namespace EasyBilling.ANAFIntegration.EFactura
{
    public class Auth
    {
        private readonly HttpClient _httpClient;

        public Auth()
        {
            _httpClient = new HttpClient();
        }

        // Future e-factura functions will be added here
        // Examples:
        // - UploadInvoiceAsync
        // - GetInvoiceStatusAsync
        // - DownloadInvoiceAsync
        // - GetMessagesAsync


    }
}

using System.Text;
using System.Text.Json;
using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Domain.Models;

namespace EasyBilling.ANAFIntegration.EFactura
{
    public class EFactura
    {
        private readonly HttpClient _httpClient;
        private const string AnafTestBaseUrl = "https://api.anaf.ro/test/FCTEL/rest";
        private const string AnafProdBaseUrl = "https://api.anaf.ro/prod/FCTEL/rest";

        public EFactura()
        {
            _httpClient = new HttpClient();
        }

        public EFactura(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Uploads an invoice XML to ANAF E-Factura system
        /// </summary>
        /// <param name="invoice">The invoice entity with Company, Client, and InvoiceLines loaded</param>
        /// <param name="xmlContent">The UBL 2.1 XML content to upload</param>
        /// <param name="accessToken">The ANAF access token for the user</param>
        /// <param name="useProduction">Whether to use production environment (default: false for test)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Upload response from ANAF</returns>
        public async Task<EFacturaUploadResponse> UploadXmlAsync(
            Invoice invoice,
            string xmlContent,
            string accessToken,
            bool useProduction = false,
            CancellationToken cancellationToken = default)
        {
            if (invoice.Company == null)
                throw new ArgumentException("Invoice must have Company information loaded", nameof(invoice));

            var baseUrl = useProduction ? AnafProdBaseUrl : AnafTestBaseUrl;
            var cui = invoice.Company.CUI.Replace("RO", "").Trim();
            var uploadUrl = $"{baseUrl}/upload?standard=UBL&cif={cui}";

            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);

            // Add authorization header
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            // Create multipart form data content
            using var content = new MultipartFormDataContent();
            var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);
            var xmlFileContent = new ByteArrayContent(xmlBytes);
            xmlFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");

            // ANAF expects the file parameter to be named "file"
            var fileName = $"{invoice.Series}{invoice.Number}_{invoice.Date:yyyyMMdd}.xml";
            content.Add(xmlFileContent, "file", fileName);

            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"ANAF API request failed with status {response.StatusCode}: {responseContent}");
            }

            var uploadResponse = JsonSerializer.Deserialize<EFacturaUploadResponse>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (uploadResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize ANAF response");
            }

            return uploadResponse;
        }

        // Future e-factura functions will be added here
        // Examples:
        // - GetInvoiceStatusAsync
        // - DownloadInvoiceAsync
        // - GetMessagesAsync
    }
}

using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Domain.Models;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

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

        /// <summary>
        /// Downloads the signed invoice ZIP from ANAF
        /// </summary>
        /// <param name="downloadId">The id_descarcare received from status check</param>
        /// <param name="accessToken">The ANAF access token</param>
        /// <param name="useProduction">Whether to use production environment</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>ZIP file as byte array containing signed invoice</returns>
        public async Task<EFacturaDownloadResponse> DownloadAsync(
            string downloadId,
            string accessToken,
            bool useProduction = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(downloadId))
                throw new ArgumentException("Download ID is required", nameof(downloadId));

            var baseUrl = useProduction ? AnafProdBaseUrl : AnafTestBaseUrl;
            var downloadUrl = $"{baseUrl}/descarcare?id={downloadId}";

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"ANAF download failed with status {response.StatusCode}: {errorContent}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var zipBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            // Check if response is XML (error) instead of ZIP
            if (contentType?.Contains("xml") == true || IsXmlContent(zipBytes))
            {
                var xmlContent = Encoding.UTF8.GetString(zipBytes);
                var errors = ParseErrorsFromXml(xmlContent);

                return new EFacturaDownloadResponse
                {
                    Success = false,
                    ErrorMessage = errors ?? "Unknown error from ANAF",
                    ZipContent = null
                };
            }

            return new EFacturaDownloadResponse
            {
                Success = true,
                ZipContent = zipBytes,
                ErrorMessage = null
            };
        }

        #region Private Helpers

        private static bool IsXmlContent(byte[] content)
        {
            if (content.Length < 5)
                return false;

            // Check for XML declaration or root element
            var start = Encoding.UTF8.GetString(content, 0, Math.Min(100, content.Length));
            return start.TrimStart().StartsWith("<?xml") || start.TrimStart().StartsWith("<");
        }

        private static string? ParseErrorsFromXml(string xmlContent)
        {
            try
            {
                var doc = XDocument.Parse(xmlContent);
                var errors = doc.Descendants()
                    .Where(e => e.Name.LocalName.Contains("Error"))
                    .Select(e => e.Attribute("errorMessage")?.Value ?? e.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .ToList();

                return errors.Any() ? string.Join("; ", errors) : null;
            }
            catch
            {
                return xmlContent.Length > 500 ? xmlContent[..500] : xmlContent;
            }
        }

        #endregion

    }
}

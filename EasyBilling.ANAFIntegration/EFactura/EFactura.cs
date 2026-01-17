using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace EasyBilling.ANAFIntegration.EFactura;

public class EFactura
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const string AnafTestBaseUrl = "https://api.anaf.ro/test/FCTEL/rest";
    private const string AnafProdBaseUrl = "https://api.anaf.ro/prod/FCTEL/rest";

    public EFactura()
    {
        _httpClient = new HttpClient();
        _logger = NullLoggerFactory.Instance.CreateLogger("EFactura");
    }

    public EFactura(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = NullLoggerFactory.Instance.CreateLogger("EFactura");
    }

    public EFactura(HttpClient httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger("EFactura");
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
        _logger.LogInformation("Starting e-Factura XML upload - InvoiceId: {InvoiceId}, Series: {Series} Nr. {Number}, Environment: {Environment}",
            invoice.Id, invoice.Series, invoice.Number, useProduction ? "Production" : "Test");

        try
        {
            if (invoice.Company == null)
            {
                _logger.LogError("Invoice {InvoiceId} missing Company information", invoice.Id);
                throw new ArgumentException("Invoice must have Company information loaded", nameof(invoice));
            }

            _logger.LogDebug("Invoice details validated - Company: {CompanyName}, CUI: {CUI}",
                invoice.Company.Name, invoice.Company.CUI);

            var baseUrl = useProduction ? AnafProdBaseUrl : AnafTestBaseUrl;
            var cui = invoice.Company.CUI.Replace("RO", "").Trim();
            var uploadUrl = $"{baseUrl}/upload?standard=UBL&cif={cui}";

            _logger.LogDebug("Preparing upload request - URL: {UploadUrl}, XMLSize: {XMLSize} bytes",
                uploadUrl, xmlContent.Length);

            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);

            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            using var content = new MultipartFormDataContent();
            var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);
            var xmlFileContent = new ByteArrayContent(xmlBytes);
            xmlFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");

            var fileName = $"{invoice.Series}{invoice.Number}_{invoice.Date:yyyyMMdd}.xml";
            content.Add(xmlFileContent, "file", fileName);

            request.Content = content;

            _logger.LogDebug("Sending XML upload request to ANAF - FileName: {FileName}", fileName);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Received ANAF response - StatusCode: {StatusCode}, ResponseLength: {ResponseLength}",
                response.StatusCode, responseContent.Length);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ANAF API upload failed - StatusCode: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);
                throw new HttpRequestException(
                    $"ANAF API request failed with status {response.StatusCode}: {responseContent}");
            }

            var uploadResponse = JsonSerializer.Deserialize<EFacturaUploadResponse>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (uploadResponse == null)
            {
                _logger.LogError("Failed to deserialize ANAF upload response");
                throw new InvalidOperationException("Failed to deserialize ANAF response");
            }

            _logger.LogInformation("XML upload successful - InvoiceId: {InvoiceId}, UploadIndex: {UploadIndex}, Success: {Success}",
                invoice.Id, uploadResponse.UploadIndex, uploadResponse.IsSuccess);

            if (!uploadResponse.IsSuccess && uploadResponse.Errors?.Any() == true)
            {
                var errorMessages = string.Join("; ", uploadResponse.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("ANAF upload returned errors - InvoiceId: {InvoiceId}, Errors: {Errors}",
                    invoice.Id, errorMessages);
            }

            return uploadResponse;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in UploadXmlAsync");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during XML upload for InvoiceId: {InvoiceId}", invoice.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading XML to ANAF for InvoiceId: {InvoiceId}", invoice.Id);
            throw;
        }
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
        _logger.LogInformation("Starting e-Factura download - DownloadId: {DownloadId}, Environment: {Environment}",
            downloadId, useProduction ? "Production" : "Test");

        try
        {
            if (string.IsNullOrWhiteSpace(downloadId))
            {
                _logger.LogWarning("DownloadAsync called with empty DownloadId");
                throw new ArgumentException("Download ID is required", nameof(downloadId));
            }

            var baseUrl = useProduction ? AnafProdBaseUrl : AnafTestBaseUrl;
            var downloadUrl = $"{baseUrl}/descarcare?id={downloadId}";

            _logger.LogDebug("Preparing download request - URL: {DownloadUrl}", downloadUrl);

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            _logger.LogDebug("Sending download request to ANAF");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            _logger.LogDebug("Received ANAF download response - StatusCode: {StatusCode}",
                response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("ANAF download failed - StatusCode: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                throw new HttpRequestException(
                    $"ANAF download failed with status {response.StatusCode}: {errorContent}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var zipBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger.LogDebug("Download response received - ContentType: {ContentType}, Size: {Size} bytes",
                contentType, zipBytes.Length);

            if (contentType?.Contains("xml") == true || IsXmlContent(zipBytes))
            {
                _logger.LogWarning("ANAF returned XML error response instead of ZIP");

                var xmlContent = Encoding.UTF8.GetString(zipBytes);
                var errors = ParseErrorsFromXml(xmlContent);

                _logger.LogWarning("Parsed ANAF error response - Errors: {Errors}", errors ?? "No error details");

                return new EFacturaDownloadResponse
                {
                    Success = false,
                    ErrorMessage = errors ?? "Unknown error from ANAF",
                    ZipContent = null
                };
            }

            _logger.LogInformation("e-Factura download successful - DownloadId: {DownloadId}, ZipSize: {Size} bytes",
                downloadId, zipBytes.Length);

            return new EFacturaDownloadResponse
            {
                Success = true,
                ZipContent = zipBytes,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading from ANAF - DownloadId: {DownloadId}", downloadId);
            throw;
        }
    }

    #region Private Helpers

    private static bool IsXmlContent(byte[] content)
    {
        if (content.Length < 5)
            return false;

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
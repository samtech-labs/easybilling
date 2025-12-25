using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.Extensions.Configuration;
using System.Buffers.Text;
using System.Text;
using System.Xml.Linq;

namespace EasyBilling.Application.Services
{
    public class AnafIntegrationService(
        IAnafTokenRepository anafTokenRepository,
        IInvoiceService invoiceService,
        IAnafIntegrationHelper anafIntegrationHelper,
        IConfiguration configuration) : IAnafIntegrationService
    {
        private readonly IAnafTokenRepository _anafTokenRepository = anafTokenRepository;
        private readonly IAnafIntegrationHelper _anafIntegrationHelper = anafIntegrationHelper;
        private readonly IInvoiceService _invoiceService = invoiceService;
        private readonly IConfiguration _config = configuration;
        private string BaseUrl => _config["Anaf:EFacturaUrl"] ?? "https://api.anaf.ro/test/FCTEL/rest";

        public async Task SaveAnafTokenAsync(AnafTokenCreateDto anafTokenCreateDto, CancellationToken cancellationToken = default)
        {
            var anafToken = new AnafToken
            {
                UserId = anafTokenCreateDto.UserId,
                AccessToken = anafTokenCreateDto.AccessToken,
                RefreshToken = anafTokenCreateDto.RefreshToken,
                AccessTokenExpiresAt = anafTokenCreateDto.AccessTokenExpiresAt,
                RefreshTokenExpiresAt = anafTokenCreateDto.RefreshTokenExpiresAt,
                ExpiresAt = anafTokenCreateDto.ExpiresAt,
                CreatedAt = anafTokenCreateDto.CreatedAt
            };

            await _anafTokenRepository.AddAsync(anafToken, cancellationToken);
        }

        public async Task UpdateAnafTokenAsync(AnafToken anafToken, CancellationToken cancellationToken = default)
        {
            await _anafTokenRepository.UpdateAsync(anafToken, cancellationToken);
        }

        public async Task<AnafToken?> GetAnafTokenByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _anafTokenRepository.GetByUserIdAsync(userId, cancellationToken);
        }

        public async Task<bool> IsTokenExpiredAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var anafToken = await _anafTokenRepository.GetByUserIdAsync(userId, cancellationToken);

            if (anafToken == null)
            {
                return true;
            }

            if (anafToken.AccessTokenExpiresAt <= DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }

        public async Task<string> UploadXmlToAnaf(Guid invoiceId, string xmlContent, CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceService.GetInvoiceAsync(invoiceId, cancellationToken);

            if (invoice == null) {
                throw new Exception("Invoice not found");
            }

            var existingSubmissions = invoice.AnafSubmissions;

            if (existingSubmissions != null && existingSubmissions.Any())
            {
                throw new Exception("Invoice has already been accepted by ANAF");
            }

            var token = await _anafIntegrationHelper.GetTokenForCifAsync(invoice.Company.CUI, cancellationToken);
            var xml = await _invoiceService.GenerateXmlForAnaf(invoiceId, cancellationToken);

            var cui = invoice.Company.CUI;
            cui = cui.StartsWith("RO") ? cui[2..] : cui;

            var uploadIndex = await UploadXmlAsync(xml, cui, token!.AccessToken, cancellationToken);

            // step 1: create and save new submission record
            // step 2: trigger background job to check status
            // step 3: return upload result

            return uploadIndex;
        }

        private async Task<string> UploadXmlAsync(string xml, string cif, string accessToken, CancellationToken ct)
        {
            var client = _anafIntegrationHelper.CreateAuthenticatedClient(accessToken);
            var url = $"{BaseUrl}/upload?standard=UBL&cif={cif}";

            var content = new StringContent(xml, Encoding.UTF8, "text/plain");
            var response = await client.PostAsync(url, content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Upload failed: {responseContent}");
            }

            var doc = XDocument.Parse(responseContent);
            var header = doc.Root;

            var executionStatus = header?.Attribute("ExecutionStatus")?.Value;
            if (executionStatus != "0")
            {
                throw new Exception($"Upload rejected: {responseContent}");
            }

            return header?.Attribute("index_incarcare")?.Value
                ?? throw new Exception($"Missing index_incarcare: {responseContent}");
        }
    }
}

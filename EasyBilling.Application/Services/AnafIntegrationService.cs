using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Helpers;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Jobs;
using EasyBilling.Application.Responses;
using EasyBilling.Domain.Models;
using Hangfire;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Xml.Linq;

namespace EasyBilling.Application.Services
{
    public class AnafIntegrationService(
        IAnafTokenRepository anafTokenRepository,
        IInvoiceService invoiceService,
        IAnafIntegrationHelper anafIntegrationHelper,
        IInvoiceAnafSubmissionService invoiceAnafSubmissionService,
        IBackgroundJobClient backgroundJobs,
        ICompanyService companyService,
        IConfiguration configuration) : IAnafIntegrationService
    {
        private readonly IAnafTokenRepository _anafTokenRepository = anafTokenRepository;
        private readonly IAnafIntegrationHelper _anafIntegrationHelper = anafIntegrationHelper;
        private readonly IInvoiceService _invoiceService = invoiceService;
        private readonly IInvoiceAnafSubmissionService _invoiceAnafSubmissionService = invoiceAnafSubmissionService;
        private readonly IBackgroundJobClient _backgroundJobs = backgroundJobs;
        private readonly ICompanyService _companyService = companyService;
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

        public async Task<AnafUploadResult> UploadXmlToAnaf(Guid invoiceId, CancellationToken cancellationToken = default)
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

            var token = await GetTokenForCifAsync(invoice.Company.CUI, cancellationToken);

            if (token == null)
            {
                throw new Exception("No ANAF token found for the company's CIF");
            }

            var xml = await _invoiceService.GenerateXmlForAnaf(invoiceId, cancellationToken);

            var cui = invoice.Company.CUI;
            cui = cui.StartsWith("RO") ? cui[2..] : cui;

            var standard = invoice.IsCreditNote ? "CN" : "UBL";

            var uploadIndex = await UploadXmlAsync(xml, standard, cui, token!.AccessToken, cancellationToken);

            var invoiceAnafSubmission = new InvoiceAnafSubmission
            {
                InvoiceId = invoiceId,
                UploadIndex = uploadIndex,
                SentXml = Encoding.UTF8.GetBytes(xml)
            };

            try
            {
                var submission = await _invoiceAnafSubmissionService.AddAsync(invoiceAnafSubmission, cancellationToken);

                if (submission != null)
                {
                    _backgroundJobs.Schedule<AnafStatusCheckJob>(
                        job => job.ExecuteAsync(submission.Id),
                        TimeSpan.FromSeconds(5));
                }
                else
                {
                    throw new Exception("Failed to create ANAF submission record");
                }

                return new AnafUploadResult
                {
                    Success = true,
                    SubmissionId = submission.Id,
                    UploadIndex = uploadIndex
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save ANAF submission record", ex);
            }
        }

        private async Task<string> UploadXmlAsync(string xml, string standard, string cif, string accessToken, CancellationToken ct)
        {
            var client = _anafIntegrationHelper.CreateAuthenticatedClient(accessToken);
            var url = $"{BaseUrl}/upload?standard={standard}&cif={cif}";

            var content = new StringContent(xml, Encoding.UTF8, "text/plain");
            var response = await client.PostAsync(url, content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Upload failed: {responseContent}");
            }

            var doc = XDocument.Parse(responseContent);
            var header = doc.Root;

            if (header == null)
                throw new Exception($"Invalid ANAF response: {responseContent}");

            var executionStatus = header.Attribute("ExecutionStatus")?.Value;
            var indexIncarcare = header.Attribute("index_incarcare")?.Value;

            if (executionStatus != "0")
            {
                XNamespace ns = "mfp:anaf:dgti:spv:respUploadFisier:v1";
                var errors = header.Descendants(ns + "Error").Select(e => e.Value).ToList();
                var errorMsg = errors.Any() ? string.Join("; ", errors) : responseContent;
                throw new Exception($"Upload rejected: {errorMsg}");
            }

            if (string.IsNullOrEmpty(indexIncarcare))
                throw new Exception($"Missing index_incarcare: {responseContent}");

            return indexIncarcare;
        }

        private async Task<AnafToken?> GetTokenForCifAsync(string cif, CancellationToken ct)
        {
            var company = await _companyService.GetCompanyByCifAsync(cif, ct);
            if (company is null)
            {
                return null;
            }

            var anafToken = await GetAnafTokenByUserIdAsync(company.UserId, ct);

            return anafToken;
        }
    }
}

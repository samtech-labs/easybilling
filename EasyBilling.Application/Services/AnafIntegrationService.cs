using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Helpers;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Jobs;
using EasyBilling.Application.Responses;
using EasyBilling.Domain.Models;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml.Linq;

namespace EasyBilling.Application.Services;

public class AnafIntegrationService(
    IAnafTokenRepository anafTokenRepository,
    IInvoiceService invoiceService,
    IAnafIntegrationHelper anafIntegrationHelper,
    IInvoiceAnafSubmissionService invoiceAnafSubmissionService,
    IBackgroundJobClient backgroundJobs,
    ICompanyService companyService,
    IConfiguration configuration,
    ILogger<AnafIntegrationService> logger) : IAnafIntegrationService
{
    private readonly IAnafTokenRepository _anafTokenRepository = anafTokenRepository;
    private readonly IAnafIntegrationHelper _anafIntegrationHelper = anafIntegrationHelper;
    private readonly IInvoiceService _invoiceService = invoiceService;
    private readonly IInvoiceAnafSubmissionService _invoiceAnafSubmissionService = invoiceAnafSubmissionService;
    private readonly IBackgroundJobClient _backgroundJobs = backgroundJobs;
    private readonly ICompanyService _companyService = companyService;
    private readonly IConfiguration _config = configuration;
    private readonly ILogger<AnafIntegrationService> _logger = logger;
    private string BaseUrl => _config["Anaf:EFacturaUrl"] ?? "https://api.anaf.ro/test/FCTEL/rest";

    public async Task SaveAnafTokenAsync(AnafTokenCreateDto anafTokenCreateDto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving ANAF token for user {UserId}", anafTokenCreateDto.UserId);

        try
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

            _logger.LogInformation("ANAF token successfully saved for user {UserId}, expires at {ExpiresAt}",
                anafTokenCreateDto.UserId, anafTokenCreateDto.AccessTokenExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving ANAF token for user {UserId}", anafTokenCreateDto.UserId);
            throw;
        }
    }

    public async Task UpdateAnafTokenAsync(AnafToken anafToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating ANAF token for user {UserId}", anafToken.UserId);

        try
        {
            await _anafTokenRepository.UpdateAsync(anafToken, cancellationToken);

            _logger.LogInformation("ANAF token successfully updated for user {UserId}, new expiry: {ExpiresAt}",
                anafToken.UserId, anafToken.AccessTokenExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ANAF token for user {UserId}", anafToken.UserId);
            throw;
        }
    }

    public async Task<AnafToken?> GetAnafTokenByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving ANAF token for user {UserId}", userId);

        try
        {
            var token = await _anafTokenRepository.GetByUserIdAsync(userId, cancellationToken);

            if (token == null)
            {
                _logger.LogWarning("No ANAF token found for user {UserId}", userId);
                return null;
            }

            _logger.LogDebug("ANAF token retrieved for user {UserId}, expires at {ExpiresAt}",
                userId, token.AccessTokenExpiresAt);

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ANAF token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsTokenExpiredAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking token expiration for user {UserId}", userId);

        try
        {
            var anafToken = await _anafTokenRepository.GetByUserIdAsync(userId, cancellationToken);

            if (anafToken == null)
            {
                _logger.LogWarning("No token found for user {UserId}, treating as expired", userId);
                return true;
            }

            var isExpired = anafToken.AccessTokenExpiresAt <= DateTime.UtcNow;

            _logger.LogDebug("Token expiration check for user {UserId}: IsExpired={IsExpired}, ExpiresAt={ExpiresAt}",
                userId, isExpired, anafToken.AccessTokenExpiresAt);

            return isExpired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking token expiration for user {UserId}", userId);
            throw;
        }
    }

    public async Task<AnafUploadResult> UploadXmlToAnaf(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting ANAF XML upload for invoice {InvoiceId}", invoiceId);

        try
        {
            var invoice = await _invoiceService.GetInvoiceAsync(invoiceId, cancellationToken);

            if (invoice == null)
            {
                _logger.LogError("Invoice {InvoiceId} not found", invoiceId);
                throw new Exception("Invoice not found");
            }

            _logger.LogDebug("Retrieved invoice {InvoiceId}, company: {CompanyName}, client: {ClientName}",
                invoiceId, invoice.Company.Name, invoice.Client.Name);

            var existingSubmissions = invoice.AnafSubmissions;

            if (existingSubmissions != null && existingSubmissions.Any())
            {
                _logger.LogWarning("Invoice {InvoiceId} has already been submitted to ANAF (existing submissions: {Count})",
                    invoiceId, existingSubmissions.Count);
                throw new Exception("Invoice has already been accepted by ANAF");
            }

            var token = await GetTokenForCifAsync(invoice.Company.CUI, cancellationToken);

            if (token == null)
            {
                _logger.LogError("No ANAF token found for company CIF {CIF}", invoice.Company.CUI);
                throw new Exception("No ANAF token found for the company's CIF");
            }

            _logger.LogDebug("ANAF token found for company {CompanyName} (CIF: {CIF})",
                invoice.Company.Name, invoice.Company.CUI);

            var xml = await _invoiceService.GenerateXmlForAnaf(invoiceId, cancellationToken);

            _logger.LogDebug("Generated ANAF XML for invoice {InvoiceId}, XML length: {XmlLength} bytes",
                invoiceId, xml.Length);

            var cui = invoice.Company.CUI;
            cui = cui.StartsWith("RO") ? cui[2..] : cui;

            var standard = invoice.IsCreditNote ? "CN" : "UBL";

            _logger.LogDebug("Uploading {Standard} format invoice {InvoiceId} with CIF {CIF}",
                standard, invoiceId, cui);

            var uploadIndex = await UploadXmlAsync(xml, standard, cui, token!.AccessToken, cancellationToken);

            _logger.LogInformation("Successfully uploaded invoice {InvoiceId} to ANAF with index {UploadIndex}",
                invoiceId, uploadIndex);

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
                    _logger.LogDebug("Created submission record {SubmissionId} for invoice {InvoiceId}",
                        submission.Id, invoiceId);

                    var jobId = _backgroundJobs.Schedule<AnafStatusCheckJob>(
                        job => job.ExecuteAsync(submission.Id),
                        TimeSpan.FromSeconds(5));

                    _logger.LogInformation("Scheduled background job {JobId} to check status of submission {SubmissionId} in 5 seconds",
                        jobId, submission.Id);

                    return new AnafUploadResult
                    {
                        Success = true,
                        SubmissionId = submission.Id,
                        UploadIndex = uploadIndex
                    };
                }
                else
                {
                    _logger.LogError("Failed to create ANAF submission record for invoice {InvoiceId}", invoiceId);
                    throw new Exception("Failed to create ANAF submission record");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ANAF submission record for invoice {InvoiceId}", invoiceId);
                throw new Exception("Failed to save ANAF submission record", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading invoice {InvoiceId} to ANAF", invoiceId);
            throw;
        }
    }

    private async Task<string> UploadXmlAsync(string xml, string standard, string cif, string accessToken, CancellationToken ct)
    {
        _logger.LogDebug("Initiating ANAF XML upload - Standard: {Standard}, CIF: {CIF}, XML size: {XmlSize} bytes",
            standard, cif, xml.Length);

        try
        {
            var client = _anafIntegrationHelper.CreateAuthenticatedClient(accessToken);
            var url = $"{BaseUrl}/upload?standard={standard}&cif={cif}";

            _logger.LogDebug("Uploading to URL: {Url}", url);

            var content = new StringContent(xml, Encoding.UTF8, "text/plain");
            var response = await client.PostAsync(url, content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("ANAF upload response - Status: {StatusCode}, Content length: {ContentLength}",
                response.StatusCode, responseContent.Length);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Upload failed with status {StatusCode}: {Response}",
                    response.StatusCode, responseContent);
                throw new Exception($"Upload failed: {responseContent}");
            }

            var doc = XDocument.Parse(responseContent);
            var header = doc.Root;

            if (header == null)
            {
                _logger.LogError("Invalid ANAF response - no root element: {Response}", responseContent);
                throw new Exception($"Invalid ANAF response: {responseContent}");
            }

            var executionStatus = header.Attribute("ExecutionStatus")?.Value;
            var indexIncarcare = header.Attribute("index_incarcare")?.Value;

            _logger.LogDebug("ANAF response parsed - ExecutionStatus: {ExecutionStatus}, IndexIncarcare: {IndexIncarcare}",
                executionStatus ?? "null", indexIncarcare ?? "null");

            if (executionStatus != "0")
            {
                XNamespace ns = "mfp:anaf:dgti:spv:respUploadFisier:v1";
                var errors = header.Descendants(ns + "Error").Select(e => e.Value).ToList();
                var errorMsg = errors.Any() ? string.Join("; ", errors) : responseContent;

                _logger.LogError("Upload rejected by ANAF - ExecutionStatus: {ExecutionStatus}, Errors: {Errors}",
                    executionStatus, errorMsg);

                throw new Exception($"Upload rejected: {errorMsg}");
            }

            if (string.IsNullOrEmpty(indexIncarcare))
            {
                _logger.LogError("ANAF response missing index_incarcare attribute: {Response}", responseContent);
                throw new Exception($"Missing index_incarcare: {responseContent}");
            }

            _logger.LogInformation("XML successfully uploaded to ANAF with index: {IndexIncarcare}", indexIncarcare);
            return indexIncarcare;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during ANAF XML upload - Standard: {Standard}, CIF: {CIF}",
                standard, cif);
            throw;
        }
    }

    private async Task<AnafToken?> GetTokenForCifAsync(string cif, CancellationToken ct)
    {
        _logger.LogDebug("Retrieving ANAF token for company CIF: {CIF}", cif);

        try
        {
            var company = await _companyService.GetCompanyByCifAsync(cif, ct);

            if (company is null)
            {
                _logger.LogWarning("Company with CIF {CIF} not found", cif);
                return null;
            }

            _logger.LogDebug("Company found: {CompanyName}, UserId: {UserId}", company.Name, company.UserId);

            var anafToken = await GetAnafTokenByUserIdAsync(company.UserId, ct);

            if (anafToken != null)
            {
                _logger.LogDebug("ANAF token found for company {CompanyName}", company.Name);
            }
            else
            {
                _logger.LogWarning("No ANAF token found for company {CompanyName} (UserId: {UserId})",
                    company.Name, company.UserId);
            }

            return anafToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ANAF token for CIF: {CIF}", cif);
            throw;
        }
    }
}

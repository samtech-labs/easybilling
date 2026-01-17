using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Helpers;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Domain.Models;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Xml.Linq;
using EasyBilling.Application.Responses;
using EasyBilling.Domain.Enums;
using System.Xml;

namespace EasyBilling.Application.Jobs;

public class AnafStatusCheckJob
{
    private readonly IInvoiceAnafSubmissionRepository _submissionRepository;
    private readonly IAnafIntegrationService _anafIntegration;
    private readonly IAnafIntegrationHelper _anafHelper;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<AnafStatusCheckJob> _logger;
    private readonly string _baseUrl;

    private const int MaxRetries = 20;

    public AnafStatusCheckJob(
        IInvoiceAnafSubmissionRepository submissionRepository,
        IAnafIntegrationService anafIntegration,
        IAnafIntegrationHelper anafHelper,
        IBackgroundJobClient backgroundJobs,
        IConfiguration config,
        ILogger<AnafStatusCheckJob> logger)
    {
        _submissionRepository = submissionRepository;
        _anafIntegration = anafIntegration;
        _anafHelper = anafHelper;
        _backgroundJobs = backgroundJobs;
        _logger = logger;

        var isTestMode = config.GetValue<bool>("Anaf:TestMode");
        _baseUrl = isTestMode
            ? "https://api.anaf.ro/test/FCTEL/rest"
            : config["Anaf:EFacturaUrl"] ?? "https://api.anaf.ro/prod/FCTEL/rest";

        _logger.LogDebug("AnafStatusCheckJob initialized - TestMode: {TestMode}, BaseUrl: {BaseUrl}",
            isTestMode, _baseUrl);
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid submissionId)
    {
        _logger.LogInformation("Starting ANAF status check for submission {SubmissionId}", submissionId);

        var submission = await _submissionRepository.GetByIdWithInvoiceAsync(submissionId);

        if (submission == null)
        {
            _logger.LogWarning("Submission {SubmissionId} not found in database", submissionId);
            return;
        }

        _logger.LogDebug("Retrieved submission {SubmissionId} with status {Status}, retry count: {RetryCount}",
            submissionId, submission.Status, submission.RetryCount);

        if (submission.Status is AnafSubmissionStatus.Ok or AnafSubmissionStatus.Error)
        {
            _logger.LogInformation("Submission {SubmissionId} already finalized with status {Status}, skipping check",
                submissionId, submission.Status);
            return;
        }

        var token = await _anafIntegration.GetAnafTokenByUserIdAsync(submission.Invoice.Company.UserId);
        if (token == null)
        {
            _logger.LogError("ANAF token not found or expired for user {UserId} (submission {SubmissionId})",
                submission.Invoice.Company.UserId, submissionId);

            submission.Status = AnafSubmissionStatus.Error;
            submission.ErrorMessage = "ANAF token not found or expired";
            await _submissionRepository.SaveChangesAsync();
            return;
        }

        _logger.LogDebug("ANAF token retrieved for user {UserId}", submission.Invoice.Company.UserId);

        try
        {
            var result = await CheckStatusAsync(submission.UploadIndex, token.AccessToken);

            submission.LastCheckedAt = DateTime.UtcNow;
            submission.RetryCount++;

            _logger.LogDebug("Status check result: Stare={Stare}, IsTechnicalError={IsTechnicalError}, ErrorMessage={Error}",
                result.Stare, result.IsTechnicalError, result.ErrorMessage ?? "none");

            if (result.IsTechnicalError)
            {
                _logger.LogWarning("Technical error from ANAF for submission {SubmissionId}: {Error}",
                    submissionId, result.ErrorMessage);

                submission.Status = AnafSubmissionStatus.Error;
                submission.ErrorMessage = result.ErrorMessage ?? "Technical error from ANAF";

                await _submissionRepository.SaveChangesAsync();
                return;
            }

            switch (result.Stare)
            {
                case "ok":
                    _logger.LogInformation("Invoice {InvoiceId} approved by ANAF, processing success handler",
                        submission.InvoiceId);
                    await HandleSuccessAsync(submission, result.IdDescarcare, token.AccessToken);
                    break;

                case "nok":
                    _logger.LogWarning("Invoice {InvoiceId} rejected by ANAF, processing error handler",
                        submission.InvoiceId);
                    await HandleErrorAsync(submission, result.IdDescarcare, token.AccessToken);
                    break;

                default:
                    _logger.LogDebug("Invoice {InvoiceId} still processing, status: {Status}",
                        submission.InvoiceId, result.Stare);
                    HandlePending(submission);
                    break;
            }

            await _submissionRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while checking ANAF status for submission {SubmissionId}",
                submissionId);

            submission.RetryCount++;
            submission.LastCheckedAt = DateTime.UtcNow;

            if (submission.RetryCount >= MaxRetries)
            {
                _logger.LogError("Max retry attempts ({MaxRetries}) exceeded for submission {SubmissionId}",
                    MaxRetries, submissionId);

                submission.Status = AnafSubmissionStatus.Error;
                submission.ErrorMessage = $"Max retries exceeded: {ex.Message}";
            }
            else
            {
                _logger.LogDebug("Scheduling retry #{RetryCount} for submission {SubmissionId}",
                    submission.RetryCount, submissionId);

                ScheduleRetry(submissionId, submission.RetryCount);
            }

            await _submissionRepository.SaveChangesAsync();
        }
    }

    private async Task<StatusResult> CheckStatusAsync(string uploadIndex, string accessToken)
    {
        _logger.LogDebug("Checking ANAF status for upload index: {UploadIndex}", uploadIndex);

        try
        {
            var client = _anafHelper.CreateAuthenticatedClient(accessToken);
            var url = $"{_baseUrl}/stareMesaj?id_incarcare={uploadIndex}";

            _logger.LogDebug("Requesting ANAF status from URL: {Url}", url);

            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("ANAF Response - Status Code: {StatusCode}, Content Length: {ContentLength}",
                response.StatusCode, content.Length);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ANAF returned non-success status code: {StatusCode}", response.StatusCode);
            }

            var doc = XDocument.Parse(content);
            var header = doc.Root;

            var stare = header?.Attribute("stare")?.Value;
            var idDescarcare = header?.Attribute("id_descarcare")?.Value;

            _logger.LogDebug("Parsed ANAF response - State: {State}, DownloadId: {DownloadId}",
                stare ?? "null", idDescarcare ?? "null");

            var errorMessages = new List<string>();

            var errorElements = header?.Descendants()
                .Where(e => e.Name.LocalName == "Error" || e.Name.LocalName == "Errors")
                .ToList() ?? [];

            _logger.LogDebug("Found {ErrorCount} error elements in response", errorElements.Count);

            foreach (var error in errorElements)
            {
                var errorMsg = error.Attribute("errorMessage")?.Value;
                if (!string.IsNullOrEmpty(errorMsg))
                {
                    errorMessages.Add(errorMsg);
                    _logger.LogDebug("Error attribute: {Error}", errorMsg);
                }

                if (!string.IsNullOrWhiteSpace(error.Value))
                {
                    errorMessages.Add(error.Value);
                    _logger.LogDebug("Error value: {Error}", error.Value);
                }
            }

            var isTechnicalError = errorMessages.Any(e =>
                e.Contains("eroare tehnica", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("Cod: 4001") ||
                e.Contains("Cod: 4002") ||
                e.Contains("Cod: 5001"));

            if (isTechnicalError)
            {
                _logger.LogWarning("Technical error detected in ANAF response");
            }

            if (string.IsNullOrEmpty(stare) && errorMessages.Any())
            {
                stare = isTechnicalError ? "technical_error" : "nok";
                _logger.LogDebug("No state in response but errors found, setting state to: {State}", stare);
            }

            return new StatusResult
            {
                Stare = stare ?? "in prelucrare",
                IdDescarcare = idDescarcare,
                ErrorMessage = errorMessages.Any() ? string.Join("; ", errorMessages.Distinct()) : null,
                IsTechnicalError = isTechnicalError
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking ANAF status for upload index: {UploadIndex}", uploadIndex);
            throw;
        }
    }

    private async Task HandleSuccessAsync(InvoiceAnafSubmission submission, string? idDescarcare, string accessToken)
    {
        _logger.LogInformation("✓ Handling success for invoice {InvoiceId} (download ID: {DownloadId})",
            submission.InvoiceId, idDescarcare ?? "null");

        submission.Status = AnafSubmissionStatus.Ok;
        submission.DownloadId = idDescarcare;

        if (!string.IsNullOrEmpty(idDescarcare))
        {
            try
            {
                _logger.LogDebug("Attempting to download signed invoice for {InvoiceId}", submission.InvoiceId);

                var client = _anafHelper.CreateAuthenticatedClient(accessToken);
                var url = $"{_baseUrl}/descarcare?id={idDescarcare}";

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    submission.SignedXml = await response.Content.ReadAsByteArrayAsync();
                    _logger.LogInformation("Successfully downloaded signed invoice for {InvoiceId}, size: {Size} bytes",
                        submission.InvoiceId, submission.SignedXml?.Length ?? 0);
                }
                else
                {
                    _logger.LogWarning("Failed to download signed invoice for {InvoiceId}, status code: {StatusCode}",
                        submission.InvoiceId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while downloading signed invoice for {InvoiceId}",
                    submission.InvoiceId);
            }
        }
        else
        {
            _logger.LogWarning("No download ID provided for successful submission {SubmissionId}",
                submission.Id);
        }
    }

    private async Task HandleErrorAsync(InvoiceAnafSubmission submission, string? idDescarcare, string accessToken)
    {
        _logger.LogWarning("✗ Handling error for invoice {InvoiceId} (download ID: {DownloadId})",
            submission.InvoiceId, idDescarcare ?? "null");

        submission.Status = AnafSubmissionStatus.Error;
        submission.DownloadId = idDescarcare;

        if (!string.IsNullOrEmpty(idDescarcare))
        {
            try
            {
                _logger.LogDebug("Attempting to download error details for {InvoiceId}", submission.InvoiceId);

                var client = _anafHelper.CreateAuthenticatedClient(accessToken);
                var url = $"{_baseUrl}/descarcare?id={idDescarcare}";

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var zipBytes = await response.Content.ReadAsByteArrayAsync();
                    submission.SignedXml = zipBytes;

                    _logger.LogDebug("Downloaded error details archive for {InvoiceId}, size: {Size} bytes",
                        submission.InvoiceId, zipBytes.Length);

                    var errors = ExtractErrorsFromZip(zipBytes);
                    submission.ErrorMessage = errors;

                    _logger.LogWarning("Extracted errors from ANAF response for {InvoiceId}: {Errors}",
                        submission.InvoiceId, errors);
                }
                else
                {
                    _logger.LogWarning("Failed to download error details for {InvoiceId}, status code: {StatusCode}",
                        submission.InvoiceId, response.StatusCode);
                    submission.ErrorMessage = $"Failed to download error details (HTTP {response.StatusCode})";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while downloading error details for {InvoiceId}",
                    submission.InvoiceId);
                submission.ErrorMessage = $"Failed to download error details from ANAF: {ex.Message}";
            }
        }
        else
        {
            _logger.LogWarning("No download ID provided for failed submission {SubmissionId}", submission.Id);
            submission.ErrorMessage = "Unknown error from ANAF (no download ID)";
        }
    }

    private string ExtractErrorsFromZip(byte[] zipBytes)
    {
        _logger.LogDebug("Extracting errors from ZIP archive ({Size} bytes)", zipBytes.Length);

        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            _logger.LogDebug("ZIP archive contains {EntryCount} entries", archive.Entries.Count);

            foreach (var entry in archive.Entries)
            {
                _logger.LogDebug("Processing ZIP entry: {Name} ({Size} bytes)", entry.Name, entry.Length);

                if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var entryStream = entry.Open();
                    using var reader = new StreamReader(entryStream);
                    var content = reader.ReadToEnd();

                    _logger.LogDebug("XML entry content length: {ContentLength}", content.Length);

                    try
                    {
                        var doc = XDocument.Parse(content);

                        var errors = doc.Descendants()
                            .Where(e => e.Name.LocalName.Contains("Error"))
                            .Select(e => e.Attribute("errorMessage")?.Value ?? e.Value)
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Distinct()
                            .ToList();

                        _logger.LogDebug("Extracted {ErrorCount} unique errors from XML", errors.Count);

                        if (errors.Any())
                        {
                            var result = string.Join("; ", errors);
                            _logger.LogInformation("Successfully extracted errors from ZIP: {Errors}", result);
                            return result;
                        }
                    }
                    catch (XmlException xex)
                    {
                        _logger.LogWarning(xex, "Failed to parse XML content from entry {Name}", entry.Name);
                        return content.Length > 2000 ? content[..2000] : content;
                    }
                }
            }

            _logger.LogWarning("No error details found in ANAF ZIP response");
            return "Error details not found in ANAF response";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract errors from ZIP archive");
            return $"Failed to parse error response: {ex.Message}";
        }
    }

    private void HandlePending(InvoiceAnafSubmission submission)
    {
        _logger.LogDebug("Invoice {InvoiceId} still in processing state, retry count: {RetryCount}/{MaxRetries}",
            submission.InvoiceId, submission.RetryCount, MaxRetries);

        submission.Status = AnafSubmissionStatus.Processing;

        if (submission.RetryCount < MaxRetries)
        {
            ScheduleRetry(submission.Id, submission.RetryCount);
        }
        else
        {
            _logger.LogError("Max retry attempts exceeded for invoice {InvoiceId}, marking as error",
                submission.InvoiceId);

            submission.Status = AnafSubmissionStatus.Error;
            submission.ErrorMessage = "Timeout: processing exceeded max wait time";
        }
    }

    private void ScheduleRetry(Guid submissionId, int retryCount)
    {
        var delay = retryCount switch
        {
            < 3 => TimeSpan.FromSeconds(5),
            < 6 => TimeSpan.FromSeconds(10),
            < 10 => TimeSpan.FromSeconds(20),
            _ => TimeSpan.FromSeconds(30)
        };

        _backgroundJobs.Schedule<AnafStatusCheckJob>(
            job => job.ExecuteAsync(submissionId),
            delay);

        _logger.LogInformation("Scheduled retry #{RetryNumber} for submission {SubmissionId} in {DelaySeconds}s",
            retryCount + 1, submissionId, delay.TotalSeconds);
    }
}

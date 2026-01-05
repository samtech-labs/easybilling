using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Helpers;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Domain.Models;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Xml.Linq;

namespace EasyBilling.Application.Jobs
{
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
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task ExecuteAsync(Guid submissionId)
        {
            _logger.LogInformation("Checking ANAF status for submission {SubmissionId}", submissionId);

            var submission = await _submissionRepository.GetByIdWithInvoiceAsync(submissionId);

            if (submission == null)
            {
                _logger.LogWarning("Submission {SubmissionId} not found", submissionId);
                return;
            }

            if (submission.Status is AnafSubmissionStatus.Ok or AnafSubmissionStatus.Error)
            {
                _logger.LogInformation("Submission {SubmissionId} already finalized: {Status}",
                    submissionId, submission.Status);
                return;
            }

            var token = await _anafIntegration.GetAnafTokenByUserIdAsync(submission.Invoice.Company.UserId);
            if (token == null)
            {
                submission.Status = AnafSubmissionStatus.Error;
                submission.ErrorMessage = "ANAF token not found or expired";
                await _submissionRepository.SaveChangesAsync();
                return;
            }

            try
            {
                var result = await CheckStatusAsync(submission.UploadIndex, token.AccessToken);

                submission.LastCheckedAt = DateTime.UtcNow;
                submission.RetryCount++;

                if (result.IsTechnicalError)
                {
                    _logger.LogWarning("Technical error from ANAF for {SubmissionId}: {Error}", submissionId, result.ErrorMessage);

                    submission.Status = AnafSubmissionStatus.Error;
                    submission.ErrorMessage = result.ErrorMessage ?? "Technical error from ANAF";

                    await _submissionRepository.SaveChangesAsync();
                    return;
                }

                switch (result.Stare)
                {
                    case "ok":
                        await HandleSuccessAsync(submission, result.IdDescarcare, token.AccessToken);
                        break;

                    case "nok":
                        await HandleErrorAsync(submission, result.IdDescarcare, token.AccessToken);
                        break;

                    default:
                        HandlePending(submission);
                        break;
                }

                await _submissionRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status for {SubmissionId}", submissionId);

                submission.RetryCount++;
                submission.LastCheckedAt = DateTime.UtcNow;

                if (submission.RetryCount >= MaxRetries)
                {
                    submission.Status = AnafSubmissionStatus.Error;
                    submission.ErrorMessage = $"Max retries exceeded: {ex.Message}";
                }
                else
                {
                    ScheduleRetry(submissionId, submission.RetryCount);
                }

                await _submissionRepository.SaveChangesAsync();
            }
        }

        private async Task<StatusResult> CheckStatusAsync(string uploadIndex, string accessToken)
        {
            var client = _anafHelper.CreateAuthenticatedClient(accessToken);
            var url = $"{_baseUrl}/stareMesaj?id_incarcare={uploadIndex}";

            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("=== ANAF Status Response ===");
            _logger.LogInformation("URL: {Url}", url);
            _logger.LogInformation("Status: {Status}", response.StatusCode);
            _logger.LogInformation("Content: {Content}", content);
            _logger.LogInformation("============================");

            var doc = XDocument.Parse(content);
            var header = doc.Root;

            var stare = header?.Attribute("stare")?.Value;
            var idDescarcare = header?.Attribute("id_descarcare")?.Value;

            // Extrage erori
            var errorMessages = new List<string>();

            var errorElements = header?.Descendants()
                .Where(e => e.Name.LocalName == "Error" || e.Name.LocalName == "Errors")
                .ToList() ?? [];

            foreach (var error in errorElements)
            {
                var errorMsg = error.Attribute("errorMessage")?.Value;
                if (!string.IsNullOrEmpty(errorMsg))
                    errorMessages.Add(errorMsg);

                if (!string.IsNullOrWhiteSpace(error.Value))
                    errorMessages.Add(error.Value);
            }

            // Detectează eroare tehnică
            var isTechnicalError = errorMessages.Any(e =>
                e.Contains("eroare tehnica", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("Cod: 4001") ||
                e.Contains("Cod: 4002") ||
                e.Contains("Cod: 5001"));

            // Dacă avem erori dar nu avem stare, e eroare
            if (string.IsNullOrEmpty(stare) && errorMessages.Any())
            {
                stare = isTechnicalError ? "technical_error" : "nok";
            }

            return new StatusResult
            {
                Stare = stare ?? "in prelucrare",
                IdDescarcare = idDescarcare,
                ErrorMessage = errorMessages.Any() ? string.Join("; ", errorMessages.Distinct()) : null,
                IsTechnicalError = isTechnicalError
            };
        }

        private async Task HandleSuccessAsync(InvoiceAnafSubmission submission, string? idDescarcare, string accessToken)
        {
            submission.Status = AnafSubmissionStatus.Ok;
            submission.DownloadId = idDescarcare;

            _logger.LogInformation("✓ Invoice {InvoiceId} validated by ANAF", submission.InvoiceId);

            if (!string.IsNullOrEmpty(idDescarcare))
            {
                try
                {
                    var client = _anafHelper.CreateAuthenticatedClient(accessToken);
                    var url = $"{_baseUrl}/descarcare?id={idDescarcare}";

                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        submission.SignedXml = await response.Content.ReadAsByteArrayAsync();
                        _logger.LogInformation("Downloaded signed invoice for {InvoiceId}", submission.InvoiceId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download signed invoice");
                }
            }
        }

        private async Task HandleErrorAsync(InvoiceAnafSubmission submission, string? idDescarcare, string accessToken)
        {
            submission.Status = AnafSubmissionStatus.Error;
            submission.DownloadId = idDescarcare;

            if (!string.IsNullOrEmpty(idDescarcare))
            {
                try
                {
                    var client = _anafHelper.CreateAuthenticatedClient(accessToken);
                    var url = $"{_baseUrl}/descarcare?id={idDescarcare}";

                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var zipBytes = await response.Content.ReadAsByteArrayAsync();
                        submission.SignedXml = zipBytes;

                        var errors = ExtractErrorsFromZip(zipBytes);
                        submission.ErrorMessage = errors;

                        _logger.LogWarning("✗ Invoice {InvoiceId} rejected by ANAF: {Errors}",
                            submission.InvoiceId, errors);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download error details");
                    submission.ErrorMessage = "Failed to download error details from ANAF";
                }
            }
            else
            {
                submission.ErrorMessage = "Unknown error from ANAF";
            }
        }

        private string ExtractErrorsFromZip(byte[] zipBytes)
        {
            try
            {
                using var zipStream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        var content = reader.ReadToEnd();

                        _logger.LogDebug("ZIP entry {Name}: {Content}", entry.Name, content);

                        try
                        {
                            var doc = XDocument.Parse(content);

                            // Caută atribute errorMessage pe elemente Error
                            var errors = doc.Descendants()
                                .Where(e => e.Name.LocalName.Contains("Error"))
                                .Select(e => e.Attribute("errorMessage")?.Value ?? e.Value)
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Distinct()
                                .ToList();

                            if (errors.Any())
                                return string.Join("; ", errors);
                        }
                        catch
                        {
                            return content.Length > 2000 ? content[..2000] : content;
                        }
                    }
                }

                return "Error details not found in ANAF response";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract errors from ZIP");
                return $"Failed to parse error response: {ex.Message}";
            }
        }

        private void HandlePending(InvoiceAnafSubmission submission)
        {
            submission.Status = AnafSubmissionStatus.Processing;

            if (submission.RetryCount < MaxRetries)
            {
                ScheduleRetry(submission.Id, submission.RetryCount);
            }
            else
            {
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

            _logger.LogDebug("Scheduled retry #{Retry} in {Delay}s", retryCount + 1, delay.TotalSeconds);
        }

        private class StatusResult
        {
            public string Stare { get; set; } = "";
            public string? IdDescarcare { get; set; }
            public string? ErrorMessage { get; set; }
            public bool IsTechnicalError { get; set; }
        }
    }
}

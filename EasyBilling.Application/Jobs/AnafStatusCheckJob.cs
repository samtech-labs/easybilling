using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Helpers;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Domain.Models;
using Hangfire;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace EasyBilling.Application.Jobs
{

    public class AnafStatusCheckJob(
        IInvoiceAnafSubmissionRepository submissionRepository,
        IAnafIntegrationService anafIntegration,
        IAnafIntegrationHelper anafHelper,
        IBackgroundJobClient backgroundJobs,
        ILogger<AnafStatusCheckJob> logger)
    {
        private readonly IInvoiceAnafSubmissionRepository _submissionRepository = submissionRepository;
        private readonly IAnafIntegrationService _anafIntegration = anafIntegration;
        private readonly IAnafIntegrationHelper _anafHelper = anafHelper;
        private readonly IBackgroundJobClient _backgroundJobs = backgroundJobs;
        private readonly ILogger<AnafStatusCheckJob> _logger = logger;

        private const int MaxRetries = 20;

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

                switch (result.Stare)
                {
                    case "ok":
                        await HandleSuccessAsync(submission, result.IdDescarcare, token.AccessToken);
                        break;

                    case "nok":
                        HandleError(submission, result.ErrorMessage);
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
            var response = await client.GetAsync($"stareMesaj?id_incarcare={uploadIndex}");
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("ANAF status response: {Content}", content);

            var doc = XDocument.Parse(content);
            var header = doc.Root;

            return new StatusResult
            {
                Stare = header?.Attribute("stare")?.Value ?? "in prelucrare",
                IdDescarcare = header?.Attribute("id_descarcare")?.Value,
                ErrorMessage = string.Join("; ", header?.Descendants("Error").Select(e => e.Value) ?? [])
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
                    var response = await client.GetAsync($"descarcare?id={idDescarcare}");

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

        private void HandleError(InvoiceAnafSubmission submission, string? errorMessage)
        {
            submission.Status = AnafSubmissionStatus.Error;
            submission.ErrorMessage = errorMessage ?? "Unknown error from ANAF";

            _logger.LogWarning("✗ Invoice {InvoiceId} rejected: {Error}",
                submission.InvoiceId, errorMessage);
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
        }
    }
}
namespace EasyBilling.Domain.Models
{
    public class InvoiceAnafSubmission
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }

        public string UploadIndex { get; set; } = null!;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public AnafSubmissionStatus Status { get; set; } = AnafSubmissionStatus.Pending;
        public string? DownloadId { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? SentXml { get; set; }
        public byte[]? SignedXml { get; set; }

        public int RetryCount { get; set; } = 0;
        public DateTime? LastCheckedAt { get; set; }

        public Invoice Invoice { get; set; } = null!;
    }

    public enum AnafSubmissionStatus
    {
        Pending,
        Processing,
        Ok,
        Error
    }
}

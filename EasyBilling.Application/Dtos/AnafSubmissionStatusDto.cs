using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Dtos;

public class AnafSubmissionStatusDto
{
    public Guid? Id { get; set; }

    public AnafSubmissionStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? UploadedAt { get; set; }

    public DateTime? LastCheckedAt { get; set; }

    public string? DownloadId { get; set; }
}

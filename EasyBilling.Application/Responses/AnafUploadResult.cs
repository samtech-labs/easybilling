namespace EasyBilling.Application.Responses;

public class AnafUploadResult
{
    public bool Success { get; set; }

    public Guid SubmissionId { get; set; }

    public string UploadIndex { get; set; } = String.Empty;

    public string? Error { get; set; }
}

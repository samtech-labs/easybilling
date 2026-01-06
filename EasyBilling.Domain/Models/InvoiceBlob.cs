namespace EasyBilling.Domain.Models;

public sealed class InvoiceBlob
{
    public Guid InvoiceId { get; set; }
    public string ContainerName { get; set; }
    public string BlobName { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
namespace EasyBilling.Domain.Models;

public class InvoiceBlob
{
    public Guid InvoiceId { get; set; }              
    public Invoice Invoice { get; set; } 

    public string ContainerName { get; set; } 
    public string BlobName { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}
namespace EasyBilling.Application.IServices;

public interface IBlobStorageService
{
    Task UploadFileToBlob(Guid invoiceId, byte[] pdfbytes, CancellationToken cancellationToken = default);
}
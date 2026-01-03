using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace EasyBilling.Application.Services;

public class BlobStorageService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly BlobContainerClient _blobContainer;
    
    public BlobStorageService(IInvoiceRepository invoiceRepository,
        BlobServiceClient blobServiceClient, 
        IConfiguration config)
    {
        _invoiceRepository = invoiceRepository;
        var containerName = config["AzureBlob:Container"]
                            ?? throw new InvalidOperationException(
                                "Missing configuration value: AzureBlob:Container");

        _blobContainer = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<(string container, string blobName)> UploadFileToBlob(Guid invoiceId, byte[] pdfbytes, CancellationToken ct = default)
    {
        if (pdfbytes == null || pdfbytes.Length == 0)
        {
            throw new ArgumentException("pdfbytes cannot be null or empty");
        }
        
        await _blobContainer.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        
        var blobName = $"{invoiceId}.pdf";
        var blob = _blobContainer.GetBlobClient(blobName);
        
        using var stream = new MemoryStream(pdfbytes);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/pdf",
                ContentDisposition = $"inline; filename=\"{invoiceId}.pdf\""
            }
        };
        try
        {
            await blob.UploadAsync(stream, options, cancellationToken: ct);
            return (_blobContainer.Name, blobName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException(
                $"Blob upload failed (Status: {ex.Status}, Code: {ex.ErrorCode})",
                ex);
        }
    }
}
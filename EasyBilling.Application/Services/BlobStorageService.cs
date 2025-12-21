using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EasyBilling.Application.Interfaces;
using EasyBilling.Application.IServices;
using EasyBilling.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace EasyBilling.Application.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly BlobContainerClient _container;
    
    public BlobStorageService(IInvoiceRepository invoiceRepository,
        BlobServiceClient blobServiceClient, 
        IConfiguration config)
    {
        _invoiceRepository = invoiceRepository;
        var containerName = config["AzureBlob:Container"] ?? "invoices";
        _container = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task UploadFileToBlob(Guid invoiceId, byte[] pdfbytes, CancellationToken ct = default)
    {
        if (pdfbytes == null || pdfbytes.Length == 0) throw new ArgumentException("pdfbytes cannot be null or empty");
        
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        
        var blobName = $"{invoiceId}.pdf";
        var blob = _container.GetBlobClient(blobName);
        
        using var stream = new MemoryStream(pdfbytes);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/pdf",
                ContentDisposition = $"inline; filename=\"{invoiceId}.pdf\""
            }
        };
            
        await blob.UploadAsync(stream, options, cancellationToken: ct);
    }
}
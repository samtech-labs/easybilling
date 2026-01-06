using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EasyBilling.Application.Interfaces.Repositories;
using Microsoft.Extensions.Configuration;

namespace EasyBilling.Application.Services;

public sealed class BlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _blobContainer;

    public BlobStorageService(
        BlobServiceClient blobServiceClient,
        IConfiguration config)
    {
        _blobServiceClient = blobServiceClient;

        var containerName = config["AzureBlob:Container"]
            ?? throw new InvalidOperationException("Missing configuration value: AzureBlob:Container");

        _blobContainer = blobServiceClient.GetBlobContainerClient(containerName);
    }

    private static string BuildInvoicePdfBlobName(Guid companyId, Guid invoiceId)
        => $"companies/{companyId}/invoices/{invoiceId}.pdf";

    /// <summary>
    /// Uploads the invoice pdf to blob storage under companies/{companyId}/invoices/{invoiceId}.pdf
    /// Returns (containerName, blobName) 
    /// </summary>
    public async Task<(string container, string blobName)> UploadInvoicePdfAsync(
        Guid companyId,
        Guid invoiceId,
        byte[] pdfBytes,
        CancellationToken ct = default)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
            throw new ArgumentException("pdfBytes cannot be null or empty", nameof(pdfBytes));

        await _blobContainer.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blobName = BuildInvoicePdfBlobName(companyId, invoiceId);
        var blob = _blobContainer.GetBlobClient(blobName);

        using var stream = new MemoryStream(pdfBytes);

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
            // overwrite: true makes retries safe if you regenerate/upload again
            await blob.UploadAsync(stream, options, cancellationToken: ct);
            return (_blobContainer.Name, blobName);
        }
        catch (OperationCanceledException) { throw; }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException(
                $"Blob upload failed (Status: {ex.Status}, Code: {ex.ErrorCode})",
                ex);
        }
    }
    
    public async Task<byte[]> DownloadInvoicePdfAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken ct = default)
    {
        var blobName = BuildInvoicePdfBlobName(companyId, invoiceId);
        return await DownloadAsync(_blobContainer.Name, blobName, ct);
    }
    
    public async Task<byte[]> DownloadAsync(
        string containerName,
        string blobName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentException("containerName cannot be null/empty", nameof(containerName));

        if (string.IsNullOrWhiteSpace(blobName))
            throw new ArgumentException("blobName cannot be null/empty", nameof(blobName));

        try
        {
            var container = containerName == _blobContainer.Name
                ? _blobContainer
                : _blobServiceClient.GetBlobContainerClient(containerName);

            var blob = container.GetBlobClient(blobName);
            
            if (!await blob.ExistsAsync(ct))
                throw new FileNotFoundException("Blob not found.", blobName);

            var download = await blob.DownloadContentAsync(ct);
            return download.Value.Content.ToArray();
        }
        catch (OperationCanceledException) { throw; }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException(
                $"Blob download failed (Status: {ex.Status}, Code: {ex.ErrorCode})",
                ex);
        }
    }
}

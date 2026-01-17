using EasyBilling.ANAFIntegration.EFactura;
using EasyBilling.ANAFIntegration.EFactura.Interfaces;
using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Application.Services;

public class EFacturaService : IEFacturaService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IAnafTokenRepository _anafTokenRepository;
    private readonly IEFacturaXmlGenerator _xmlGenerator;
    private readonly EFactura _eFactura;
    private readonly ILogger<EFacturaService> _logger;

    public EFacturaService(
        IInvoiceRepository invoiceRepository,
        IAnafTokenRepository anafTokenRepository,
        IEFacturaXmlGenerator xmlGenerator,
        EFactura eFactura,
        ILogger<EFacturaService> logger)
    {
        _invoiceRepository = invoiceRepository;
        _anafTokenRepository = anafTokenRepository;
        _xmlGenerator = xmlGenerator;
        _eFactura = eFactura;
        _logger = logger;
    }

    public async Task<EFacturaUploadResponse> GenerateAndUploadInvoiceAsync(
        Guid invoiceId,
        Guid companyId,
        bool useProduction = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting e-Factura generation and upload for invoice {InvoiceId}, company {CompanyId}, environment: {Environment}",
            invoiceId, companyId, useProduction ? "Production" : "Test");

        try
        {
            _logger.LogDebug("Retrieving invoice {InvoiceId} with details", invoiceId);

            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken);

            if (invoice == null)
            {
                _logger.LogError("Invoice {InvoiceId} not found", invoiceId);
                throw new InvalidOperationException($"Invoice with ID '{invoiceId}' not found.");
            }

            _logger.LogDebug("Invoice retrieved: Series={Series}, Number={Number}, Company={CompanyName}, Client={ClientName}",
                invoice.Series, invoice.Number, invoice.Company?.Name, invoice.Client?.Name);

            if (invoice.CompanyId != companyId)
            {
                _logger.LogWarning("Invoice {InvoiceId} does not belong to company {CompanyId}. Actual company: {ActualCompanyId}",
                    invoiceId, companyId, invoice.CompanyId);
                throw new InvalidOperationException("Invoice does not belong to the specified company.");
            }

            if (invoice.Company == null || invoice.Client == null)
            {
                _logger.LogError("Invoice {InvoiceId} missing company or client information", invoiceId);
                throw new InvalidOperationException("Invoice must have Company and Client information loaded.");
            }

            var userId = invoice.Company.UserId;

            _logger.LogDebug("Retrieved user ID {UserId} from company {CompanyId}", userId, invoice.Company.Id);

            _logger.LogDebug("Retrieving ANAF token for user {UserId}", userId);

            var anafToken = await _anafTokenRepository.GetByUserIdAsync(userId, cancellationToken);

            if (anafToken == null)
            {
                _logger.LogError("No ANAF token found for user {UserId}", userId);
                throw new InvalidOperationException("No ANAF token found for this user. Please authenticate with ANAF first.");
            }

            _logger.LogDebug("ANAF token found for user {UserId}, expires at {ExpiresAt}", userId, anafToken.AccessTokenExpiresAt);

            if (anafToken.AccessTokenExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("ANAF access token expired for user {UserId}. Expiration: {ExpirationTime}",
                    userId, anafToken.AccessTokenExpiresAt);
                throw new InvalidOperationException("ANAF access token has expired. Please re-authenticate with ANAF.");
            }

            _logger.LogDebug("ANAF token is valid, will expire at {ExpiresAt}", anafToken.AccessTokenExpiresAt);

            _logger.LogDebug("Generating XML for invoice {InvoiceId}", invoiceId);

            var xmlContent = _xmlGenerator.GenerateXml(invoice);

            _logger.LogDebug("XML generated successfully for invoice {InvoiceId}, size: {XmlSize} bytes", invoiceId, xmlContent.Length);

            _logger.LogInformation("Uploading XML to ANAF for invoice {InvoiceId}, using {Environment} environment",
                invoiceId, useProduction ? "Production" : "Test");

            var uploadResponse = await _eFactura.UploadXmlAsync(
                invoice,
                xmlContent,
                anafToken.AccessToken,
                useProduction,
                cancellationToken);

            _logger.LogInformation("E-Factura upload completed for invoice {InvoiceId}. Success: {Success}, UploadIndex: {UploadIndex}",
                invoiceId, uploadResponse.IsSuccess, uploadResponse.UploadIndex);

            if (!uploadResponse.IsSuccess)
            {
                _logger.LogWarning("E-Factura upload failed for invoice {InvoiceId}. Errors: {Errors}",
                    invoiceId, string.Join("; ", uploadResponse.Errors?.Select(e => e.ErrorMessage) ?? []));
            }

            return uploadResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating and uploading e-Factura for invoice {InvoiceId}, company {CompanyId}",
                invoiceId, companyId);
            throw;
        }
    }

    public async Task<EFacturaDownloadResponse> DownloadAnafSignedInvoiceAsync(
        Guid invoiceId,
        Guid companyId,
        string downloadId,
        bool useProduction = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting e-Factura download for invoice {InvoiceId}, company {CompanyId}, downloadId: {DownloadId}, environment: {Environment}",
            invoiceId, companyId, downloadId, useProduction ? "Production" : "Test");

        try
        {
            _logger.LogDebug("Retrieving invoice {InvoiceId}", invoiceId);

            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

            if (invoice == null)
            {
                _logger.LogError("Invoice {InvoiceId} not found", invoiceId);
                throw new InvalidOperationException($"Invoice with ID '{invoiceId}' not found.");
            }

            _logger.LogDebug("Invoice retrieved: Series={Series}, Number={Number}", invoice.Series, invoice.Number);

            if (invoice.CompanyId != companyId)
            {
                _logger.LogWarning("Invoice {InvoiceId} does not belong to company {CompanyId}. Actual company: {ActualCompanyId}",
                    invoiceId, companyId, invoice.CompanyId);
                throw new InvalidOperationException("Invoice does not belong to the specified company.");
            }
            var userId = invoice.Company.UserId;

            _logger.LogDebug("Retrieved user ID {UserId} from company {CompanyId}", userId, invoice.Company.Id);

            _logger.LogDebug("Retrieving ANAF token for user {UserId}", userId);

            var anafToken = await _anafTokenRepository.GetByUserIdAsync(userId, cancellationToken);

            if (anafToken == null)
            {
                _logger.LogError("No ANAF token found for user {UserId}", userId);
                throw new InvalidOperationException("No ANAF token found for this user. Please authenticate with ANAF first.");
            }

            _logger.LogDebug("ANAF token found for user {UserId}, expires at {ExpiresAt}", userId, anafToken.AccessTokenExpiresAt);

            if (anafToken.AccessTokenExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("ANAF access token expired for user {UserId}. Expiration: {ExpirationTime}",
                    userId, anafToken.AccessTokenExpiresAt);
                throw new InvalidOperationException("ANAF access token has expired. Please re-authenticate with ANAF.");
            }

            _logger.LogDebug("ANAF token is valid, will expire at {ExpiresAt}", anafToken.AccessTokenExpiresAt);

            var accessToken = anafToken.AccessToken;

            _logger.LogInformation("Downloading signed invoice from ANAF for invoice {InvoiceId}, downloadId: {DownloadId}",
                invoiceId, downloadId);

            var downloadResponse = await _eFactura.DownloadAsync(
                downloadId,
                accessToken,
                useProduction,
                cancellationToken);

            _logger.LogInformation("E-Factura download completed for invoice {InvoiceId}. Success: {Success}",
                invoiceId, downloadResponse.Success);

            if (!downloadResponse.Success)
            {
                _logger.LogWarning("E-Factura download failed for invoice {InvoiceId}, downloadId: {DownloadId}. Error: {ErrorMessage}",
                    invoiceId, downloadId, downloadResponse.ErrorMessage);
            }
            else
            {
                _logger.LogDebug("Downloaded file size: {FileSize} bytes", downloadResponse.ZipContent?.Length ?? 0);
            }

            return downloadResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading signed invoice for invoice {InvoiceId}, downloadId: {DownloadId}",
                invoiceId, downloadId);
            throw;
        }
    }
}

using EasyBilling.ANAFIntegration.EFactura;
using EasyBilling.ANAFIntegration.EFactura.Interfaces;
using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;

namespace EasyBilling.Application.Services
{
    public class EFacturaService : IEFacturaService
    {
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IAnafTokenRepository _anafTokenRepository;
        private readonly IEFacturaXmlGenerator _xmlGenerator;
        private readonly EFactura _eFactura;

        public EFacturaService(
            IInvoiceRepository invoiceRepository,
            IAnafTokenRepository anafTokenRepository,
            IEFacturaXmlGenerator xmlGenerator,
            EFactura eFactura)
        {
            _invoiceRepository = invoiceRepository;
            _anafTokenRepository = anafTokenRepository;
            _xmlGenerator = xmlGenerator;
            _eFactura = eFactura;
        }

        public async Task<EFacturaUploadResponse> GenerateAndUploadInvoiceAsync(
            Guid invoiceId,
            Guid companyId,
            bool useProduction = false,
            CancellationToken cancellationToken = default)
        {
            // Get invoice with all related entities
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken);

            if (invoice == null)
            {
                throw new InvalidOperationException($"Invoice with ID '{invoiceId}' not found.");
            }

            if (invoice.CompanyId != companyId)
            {
                throw new InvalidOperationException("Invoice does not belong to the specified company.");
            }

            if (invoice.Company == null || invoice.Client == null)
            {
                throw new InvalidOperationException("Invoice must have Company and Client information loaded.");
            }

            // Get the user ID from the company
            var userId = invoice.Company.UserId;

            // Get the ANAF token for the user
            var anafToken = await _anafTokenRepository.GetByUserIdAsync(userId, cancellationToken);

            if (anafToken == null)
            {
                throw new InvalidOperationException("No ANAF token found for this user. Please authenticate with ANAF first.");
            }

            // Check if access token is expired
            if (anafToken.AccessTokenExpiresAt <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("ANAF access token has expired. Please re-authenticate with ANAF.");
            }

            // Generate the XML
            var xmlContent = _xmlGenerator.GenerateXml(invoice);

            // Upload to ANAF
            var uploadResponse = await _eFactura.UploadXmlAsync(
                invoice,
                xmlContent,
                anafToken.AccessToken,
                useProduction,
                cancellationToken);

            return uploadResponse;
        }
    }
}

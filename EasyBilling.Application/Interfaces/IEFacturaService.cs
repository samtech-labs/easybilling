using EasyBilling.ANAFIntegration.EFactura.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IEFacturaService
    {
        /// <summary>
        /// Generates XML for an invoice and uploads it to ANAF E-Factura
        /// </summary>
        /// <param name="invoiceId">The invoice ID to upload</param>
        /// <param name="companyId">The company ID for authorization</param>
        /// <param name="useProduction">Whether to use production environment</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Upload response from ANAF</returns>
        Task<EFacturaUploadResponse> GenerateAndUploadInvoiceAsync(
            Guid invoiceId,
            Guid companyId,
            bool useProduction = false,
            CancellationToken cancellationToken = default);
    }
}

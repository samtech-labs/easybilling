using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;
using EasyBilling.Application.Responses;

namespace EasyBilling.Application.Interfaces.Services
{
    public interface IInvoiceService
    {
        Task<InvoiceResponseDto> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
        Task<InvoiceResponseDto> GetInvoiceByIdAsync(Guid invoiceId, Guid companyId, CancellationToken cancellationToken = default);
        Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
        Task<List<InvoiceResponseDto>> GetInvoicesByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<LastInvoiceNumberDto> GetLastInvoiceNumberAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default);
        Task<string> GenerateXmlForAnaf(Guid invoiceId, CancellationToken cancellationToken = default);
        Task<AnafSubmissionStatusDto> GetAnafSubmissionStatusAsync(Guid invoiceId, CancellationToken cancellationToken = default);
        Task<EFacturaDownloadResponse> DownloadAnafResponseAsync(Guid invoiceId, CancellationToken cancellationToken = default);
        Task<PagedResponse<InvoiceResponseDto>> GetPagedInvoicesByCompanyAsync(
            Guid companyId, PageRequest page, CancellationToken ct = default);

    }
}

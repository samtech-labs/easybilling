using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;

namespace EasyBilling.Application.Interfaces
{
    public interface IInvoiceService
    {
        Task<InvoiceResponseDto> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
        Task<InvoiceResponseDto> GetInvoiceByIdAsync(Guid invoiceId, Guid companyId, CancellationToken cancellationToken = default);
        Task<List<InvoiceResponseDto>> GetInvoicesByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    }
}

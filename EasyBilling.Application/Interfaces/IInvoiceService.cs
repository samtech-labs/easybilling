using EasyBilling.Application.Dtos;
using EasyBilling.Application.Requests;

namespace EasyBilling.Application.Interfaces
{
    public interface IInvoiceService
    {
        Task<InvoiceResponseDto> CreateInvoiceAsync(CreateInvoiceRequest request, Guid companyId);
        Task<InvoiceResponseDto> GetInvoiceByIdAsync(Guid invoiceId, Guid companyId);
        Task<List<InvoiceResponseDto>> GetInvoicesByCompanyIdAsync(Guid companyId);
        Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId);
    }
}

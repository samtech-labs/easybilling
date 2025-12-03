
namespace EasyBilling.Application.IServices
{
    public interface IInvoiceService
    {
        public Task<byte[]> CreateInvoiceAsync(Guid companyId);
    }
}

namespace EasyBilling.Application.Interfaces
{
    public interface IInvoiceService
    {
        public Task<byte[]> CreateInvoiceAsync(Guid companyId);
    }
}

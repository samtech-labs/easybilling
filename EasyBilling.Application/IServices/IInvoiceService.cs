

namespace EasyBilling.Application.IServices
{
    public interface IInvoiceService
    {
        public void GenerateInvoice(Guid companyId);
    }
}

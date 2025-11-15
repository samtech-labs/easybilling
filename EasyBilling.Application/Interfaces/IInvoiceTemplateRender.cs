using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Interfaces
{
    public interface IInvoiceTemplateRender
    {
        Task<string> RenderAsync(Company company);
    }
}

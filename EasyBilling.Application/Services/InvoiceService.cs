using EasyBilling.Application.Interfaces;
using EasyBilling.Application.IServices;

namespace EasyBilling.Application.Services
{
    public class InvoiceService(ICompanyRepository companyRepository, IInvoiceTemplateRender invoiceTemplateRender) : IInvoiceService
    {
        public readonly ICompanyRepository _companyRepository = companyRepository;
        public readonly IInvoiceTemplateRender _invoiceTemplateRender = invoiceTemplateRender;
        public async Task<byte[]> CreateInvoiceAsync(Guid companyId)
        {
            var company = await _companyRepository.GetByIdAsync(companyId);

            if (company is null)
            {
                throw new Exception("Company not found");
            }

            // Logic to generate invoice using company details
            var invoiceHtml = await _invoiceTemplateRender.RenderAsync(company);

            byte[] result;
            using (MemoryStream ms = new MemoryStream())
            {
                var pdf = TheArtOfDev.HtmlRenderer.PdfSharp.PdfGenerator.GeneratePdf(invoiceHtml, PdfSharp.PageSize.A4);
                pdf.Save(ms);
                result = ms.ToArray();
            }
            return result;
        }
    }
}

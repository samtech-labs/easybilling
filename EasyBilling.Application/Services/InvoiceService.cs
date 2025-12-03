using EasyBilling.Application.Interfaces;
using EasyBilling.Application.IServices;
using Syncfusion.HtmlConverter;
using PdfDocument = Syncfusion.Pdf.PdfDocument;

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

            HtmlToPdfConverter htmlConverter = new HtmlToPdfConverter();
            PdfDocument invoicePdf = htmlConverter.Convert(invoiceHtml, "");

            using var stream = new MemoryStream();
            invoicePdf.Save(stream);
            invoicePdf.Close(true);
            htmlConverter.Close();

            return stream.ToArray();
        }
    }
}

using EasyBilling.Application.IServices;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoiceController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        // TODO this also should include requests json body with invoice details
        [HttpPost("pdf")]
        public async Task<IActionResult> CreateInvoice(Guid companyId)
        {
            var invoicePdf = await _invoiceService.CreateInvoiceAsync(companyId);

            return File(invoicePdf, "application/pdf", "invoice.pdf");
        }

        [HttpGet]
        public IActionResult GetInvoice()
        {
            return Ok("Hello world");
        }
    }
}

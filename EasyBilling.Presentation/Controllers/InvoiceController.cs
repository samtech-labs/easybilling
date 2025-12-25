using EasyBilling.ANAFIntegration.EFactura.Interfaces;
using EasyBilling.Application.Interfaces;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvoiceController(
        IInvoiceService invoiceService,
        IInvoiceRepository invoiceRepository,
        IEFacturaXmlGenerator eFacturaXmlGenerator,
        IEFacturaService eFacturaService) : ControllerBase
    {
        private readonly IInvoiceService _invoiceService = invoiceService;
        private readonly IInvoiceRepository _invoiceRepository = invoiceRepository;
        private readonly IEFacturaXmlGenerator _eFacturaXmlGenerator = eFacturaXmlGenerator;
        private readonly IEFacturaService _eFacturaService = eFacturaService;

        [HttpPost]
        [Route("CreateInvoice")]
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.CreateInvoiceAsync(request, cancellationToken);
                return Ok(invoice);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while creating the invoice.");
            }
        }

        [HttpGet]
        [Route("GetInvoice")]
        public async Task<IActionResult> GetInvoice(Guid invoiceId, Guid companyId, CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId, companyId, cancellationToken);
                return Ok(invoice);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving the invoice.");
            }
        }

        [HttpGet]
        [Route("GetAllInvoices")]
        public async Task<IActionResult> GetAllInvoices(Guid companyId, CancellationToken cancellationToken)
        {
            try
            {
                var invoices = await _invoiceService.GetInvoicesByCompanyIdAsync(companyId, cancellationToken);
                return Ok(invoices);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
               return StatusCode(500, "An error occurred while retrieving invoices.");
            }
        }

        [HttpGet]
        [Route("GeneratePdf")]
        public async Task<IActionResult> GeneratePdf(Guid invoiceId, CancellationToken cancellationToken)
        {
            try
            {
                var pdfBytes = await _invoiceService.GenerateInvoicePdfAsync(invoiceId, cancellationToken);
                return File(pdfBytes, "application/pdf", "invoice.pdf");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while generating the invoice PDF.");
            }
        }
    }
}

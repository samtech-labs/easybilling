using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Presentation.Authorization;
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
        IAnafIntegrationService anafIntegrationService) : ControllerBase
    {
        private readonly IInvoiceService _invoiceService = invoiceService;
        private readonly IInvoiceRepository _invoiceRepository = invoiceRepository;
        private readonly IAnafIntegrationService _anafIntegrationService = anafIntegrationService;

        [HttpPost]
        [Route("CreateInvoice")]
        [Authorize(Policy = Policies.CanCreateInvoice)]
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
        public async Task<IActionResult> GetAllInvoices(
            [FromQuery] Guid companyId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string sortOrder = "desc",
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (pageNumber != 1 || pageSize != 10 || !string.IsNullOrEmpty(searchTerm) || !string.IsNullOrEmpty(sortBy) || sortOrder != "desc" || dateFrom.HasValue || dateTo.HasValue)
                {
                    var filter = new InvoicePaginationFilter
                    {
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        SearchTerm = searchTerm,
                        SortBy = sortBy,
                        SortOrder = sortOrder,
                        DateFrom = dateFrom,
                        DateTo = dateTo
                    };

                    if (!filter.IsValid)
                    {
                        return BadRequest(new { message = "Invalid pagination parameters. PageNumber must be >= 1 and PageSize must be between 1 and 100." });
                    }

                    var result = await _invoiceService.GetInvoicesByCompanyIdPaginatedAsync(companyId, filter, cancellationToken);
                    return Ok(result);
                }

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
        [Route("GetAllCreditNotes")]
        public async Task<IActionResult> GetAllCreditNotes(
            [FromQuery] Guid companyId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string sortOrder = "desc",
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (pageNumber != 1 || pageSize != 10 || !string.IsNullOrEmpty(searchTerm) || !string.IsNullOrEmpty(sortBy) || sortOrder != "desc" || dateFrom.HasValue || dateTo.HasValue)
                {
                    var filter = new InvoicePaginationFilter
                    {
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        SearchTerm = searchTerm,
                        SortBy = sortBy,
                        SortOrder = sortOrder,
                        DateFrom = dateFrom,
                        DateTo = dateTo
                    };

                    if (!filter.IsValid)
                    {
                        return BadRequest(new { message = "Invalid pagination parameters. PageNumber must be >= 1 and PageSize must be between 1 and 100." });
                    }

                    var result = await _invoiceService.GetCreditNotesByCompanyIdPaginatedAsync(companyId, filter, cancellationToken);
                    return Ok(result);
                }

                var creditNotes = await _invoiceService.GetCreditNotesByCompanyIdAsync(companyId, cancellationToken);
                return Ok(creditNotes);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving credit notes.");
            }
        }

        [HttpGet]
        [Route("GetLastInvoiceNumber")]
        public async Task<IActionResult> GetLastInvoiceNumber(Guid companyId, CancellationToken cancellationToken)
        {
            try
            {
                var lastInvoiceNumber = await _invoiceService.GetLastInvoiceNumberAsync(companyId, cancellationToken);
                return Ok(lastInvoiceNumber);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving the last invoice number.");
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

        [HttpGet]
        [Route("DownloadXml")]
        public async Task<IActionResult> DownloadXml(Guid invoiceId, CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceAsync(invoiceId, cancellationToken);
                if (invoice == null)
                {
                    return NotFound(new { message = "Invoice not found." });
                }

                var xml = await _invoiceService.GenerateXmlForAnaf(invoiceId, cancellationToken);
                var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);
                var fileName = $"{invoice.Series}_{invoice.Number}.xml";

                return File(xmlBytes, "application/xml", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while generating the invoice XML.");
            }
        }

        [HttpPost]
        [Route("SendEFactura")]
        [Authorize(Policy = Policies.CanUseEFactura)]
        public async Task<IActionResult> SendEFactura(Guid invoiceId, CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceAsync(invoiceId, cancellationToken);
                if (invoice == null)
                {
                    return NotFound(new { message = "Invoice not found." });
                }
                var uploadInvoiceResult = await _anafIntegrationService.UploadXmlToAnaf(invoiceId, cancellationToken);
                return Ok(uploadInvoiceResult);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while sending the invoice to ANAF.");
            }
        }

        [HttpGet]
        [Route("GetAnafSubmissionStatus")]
        public async Task<IActionResult> GetAnafSubmissionStatus(Guid invoiceId, CancellationToken cancellationToken)
        {
            try
            {
                var status = await _invoiceService.GetAnafSubmissionStatusAsync(invoiceId, cancellationToken);
                return Ok(status);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving the ANAF submission status.");
            }
        }

        [HttpGet]
        [Route("DownloadAnafResponse")]
        public async Task<IActionResult> DownloadAnafResponse(Guid invoiceId, CancellationToken cancellationToken)
        {
            try
            {
                var downloadResponse = await _invoiceService.DownloadAnafResponseAsync(invoiceId, cancellationToken);

                var invoice = await _invoiceService.GetInvoiceAsync(invoiceId, cancellationToken);
                var fileName = invoice != null
                    ? $"ANAF_Response_{invoice.Series}_{invoice.Number}.zip"
                    : $"ANAF_Response_{invoiceId}.zip";

                return File(downloadResponse.ZipContent, "application/zip", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while downloading the ANAF response.");
            }
        }

        [HttpPost]
        [Route("CreateCreditNote")]
        public async Task<IActionResult> CreateCreditNote([FromBody] CreateCreditNoteRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var creditNote = await _invoiceService.CreateCreditNoteAsync(request, cancellationToken);
                return Ok(creditNote);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while creating the credit note.");
            }
        }
    }
}

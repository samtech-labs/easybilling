using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class InvoiceController(
    IInvoiceService invoiceService,
    IInvoiceRepository invoiceRepository,
    IAnafIntegrationService anafIntegrationService,
    ILogger<InvoiceController> logger) : ControllerBase
{
    private readonly IInvoiceService _invoiceService = invoiceService;
    private readonly IInvoiceRepository _invoiceRepository = invoiceRepository;
    private readonly IAnafIntegrationService _anafIntegrationService = anafIntegrationService;
    private readonly ILogger<InvoiceController> _logger = logger;

    [HttpPost]
    [Route("CreateInvoice")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CreateInvoice request - Company: {CompanyId}, Series: {Series}, Number: {Number}",
            request?.CompanyId, request?.Series ?? "unknown", request?.Number ?? 0);

        try
        {
            if (request == null)
            {
                _logger.LogWarning("CreateInvoice request with null request body");
                return BadRequest(new { message = "Request body is required" });
            }

            if (request.CompanyId == Guid.Empty)
            {
                _logger.LogWarning("CreateInvoice request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            _logger.LogDebug("Creating invoice for company {CompanyId}", request.CompanyId);

            var invoice = await _invoiceService.CreateInvoiceAsync(request, cancellationToken);

            _logger.LogInformation("Invoice successfully created - InvoiceId: {InvoiceId}, Series: {Series} Nr. {Number}",
                invoice.Id, invoice.Series, invoice.Number);

            return Ok(invoice);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error creating invoice for company {CompanyId}",
                request?.CompanyId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invoice for company {CompanyId}",
                request?.CompanyId);
            return StatusCode(500, new { message = "An error occurred while creating the invoice." });
        }
    }

    [HttpGet]
    [Route("GetInvoice")]
    public async Task<IActionResult> GetInvoice(Guid invoiceId, Guid companyId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetInvoice request - InvoiceId: {InvoiceId}, CompanyId: {CompanyId}",
            invoiceId, companyId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("GetInvoice request with empty InvoiceId");
                return BadRequest(new { message = "InvoiceId is required" });
            }

            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("GetInvoice request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            _logger.LogDebug("Retrieving invoice {InvoiceId} for company {CompanyId}", invoiceId, companyId);

            var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId, companyId, cancellationToken);

            _logger.LogInformation("Invoice retrieved - InvoiceId: {InvoiceId}, Series: {Series} Nr. {Number}",
                invoiceId, invoice.Series, invoice.Number);

            return Ok(invoice);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error retrieving invoice {InvoiceId}",
                invoiceId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoice {InvoiceId} for company {CompanyId}",
                invoiceId, companyId);
            return StatusCode(500, new { message = "An error occurred while retrieving the invoice." });
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
        _logger.LogInformation("GetAllInvoices request for company {CompanyId} - PageNumber: {PageNumber}, PageSize: {PageSize}",
            companyId, pageNumber, pageSize);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("GetAllInvoices request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            if (pageNumber != 1 || pageSize != 10 || !string.IsNullOrEmpty(searchTerm) || !string.IsNullOrEmpty(sortBy) || sortOrder != "desc" || dateFrom.HasValue || dateTo.HasValue)
            {
                _logger.LogDebug("Applying pagination filters - SearchTerm: {SearchTerm}, SortBy: {SortBy}, DateFrom: {DateFrom}, DateTo: {DateTo}",
                    searchTerm ?? "none", sortBy ?? "none",
                    dateFrom?.ToString("yyyy-MM-dd") ?? "none",
                    dateTo?.ToString("yyyy-MM-dd") ?? "none");

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
                    _logger.LogWarning("Invalid pagination parameters - PageNumber: {PageNumber}, PageSize: {PageSize}",
                        pageNumber, pageSize);
                    return BadRequest(new { message = "Invalid pagination parameters. PageNumber must be >= 1 and PageSize must be between 1 and 100." });
                }

                var result = await _invoiceService.GetInvoicesByCompanyIdPaginatedAsync(companyId, filter, cancellationToken);

                _logger.LogInformation("Retrieved {ItemCount} invoices out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                    result.Items.Count, result.TotalCount, companyId, pageNumber);

                return Ok(result);
            }

            _logger.LogDebug("Retrieving all invoices for company {CompanyId}", companyId);

            var invoices = await _invoiceService.GetInvoicesByCompanyIdAsync(companyId, cancellationToken);

            _logger.LogInformation("Retrieved {InvoiceCount} invoices for company {CompanyId}",
                invoices.Count, companyId);

            return Ok(invoices);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error retrieving invoices for company {CompanyId}",
                companyId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoices for company {CompanyId}", companyId);
            return StatusCode(500, new { message = "An error occurred while retrieving invoices." });
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
        _logger.LogInformation("GetAllCreditNotes request for company {CompanyId} - PageNumber: {PageNumber}, PageSize: {PageSize}",
            companyId, pageNumber, pageSize);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("GetAllCreditNotes request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            if (pageNumber != 1 || pageSize != 10 || !string.IsNullOrEmpty(searchTerm) || !string.IsNullOrEmpty(sortBy) || sortOrder != "desc" || dateFrom.HasValue || dateTo.HasValue)
            {
                _logger.LogDebug("Applying pagination filters - SearchTerm: {SearchTerm}, SortBy: {SortBy}, DateFrom: {DateFrom}, DateTo: {DateTo}",
                    searchTerm ?? "none", sortBy ?? "none",
                    dateFrom?.ToString("yyyy-MM-dd") ?? "none",
                    dateTo?.ToString("yyyy-MM-dd") ?? "none");

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
                    _logger.LogWarning("Invalid pagination parameters - PageNumber: {PageNumber}, PageSize: {PageSize}",
                        pageNumber, pageSize);
                    return BadRequest(new { message = "Invalid pagination parameters. PageNumber must be >= 1 and PageSize must be between 1 and 100." });
                }

                var result = await _invoiceService.GetCreditNotesByCompanyIdPaginatedAsync(companyId, filter, cancellationToken);

                _logger.LogInformation("Retrieved {ItemCount} credit notes out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                    result.Items.Count, result.TotalCount, companyId, pageNumber);

                return Ok(result);
            }

            _logger.LogDebug("Retrieving all credit notes for company {CompanyId}", companyId);

            var creditNotes = await _invoiceService.GetCreditNotesByCompanyIdAsync(companyId, cancellationToken);

            _logger.LogInformation("Retrieved {CreditNoteCount} credit notes for company {CompanyId}",
                creditNotes.Count, companyId);

            return Ok(creditNotes);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error retrieving credit notes for company {CompanyId}",
                companyId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving credit notes for company {CompanyId}", companyId);
            return StatusCode(500, new { message = "An error occurred while retrieving credit notes." });
        }
    }

    [HttpGet]
    [Route("GetLastInvoiceNumber")]
    public async Task<IActionResult> GetLastInvoiceNumber(Guid companyId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("GetLastInvoiceNumber request for company {CompanyId}", companyId);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("GetLastInvoiceNumber request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            var lastInvoiceNumber = await _invoiceService.GetLastInvoiceNumberAsync(companyId, cancellationToken);

            _logger.LogInformation("Last invoice number retrieved for company {CompanyId}: Series={Series}, Number={Number}",
                companyId, lastInvoiceNumber.Series, lastInvoiceNumber.Number);

            return Ok(lastInvoiceNumber);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error retrieving last invoice number for company {CompanyId}",
                companyId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last invoice number for company {CompanyId}", companyId);
            return StatusCode(500, new { message = "An error occurred while retrieving the last invoice number." });
        }
    }

    [HttpGet]
    [Route("GeneratePdf")]
    public async Task<IActionResult> GeneratePdf(Guid invoiceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GeneratePdf request for invoice {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("GeneratePdf request with empty InvoiceId");
                return BadRequest(new { message = "InvoiceId is required" });
            }

            _logger.LogDebug("Generating PDF for invoice {InvoiceId}", invoiceId);

            var pdfBytes = await _invoiceService.GenerateInvoicePdfAsync(invoiceId, cancellationToken);

            _logger.LogInformation("PDF successfully generated for invoice {InvoiceId}, size: {Size} bytes",
                invoiceId, pdfBytes.Length);

            return File(pdfBytes, "application/pdf", "invoice.pdf");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error generating PDF for invoice {InvoiceId}",
                invoiceId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF for invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { message = "An error occurred while generating the invoice PDF." });
        }
    }

    [HttpPost]
    [Route("SendEFactura")]
    public async Task<IActionResult> SendEFactura(Guid invoiceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SendEFactura request for invoice {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("SendEFactura request with empty InvoiceId");
                return BadRequest(new { message = "InvoiceId is required" });
            }

            _logger.LogDebug("Retrieving invoice {InvoiceId} for e-Factura submission", invoiceId);

            var invoice = await _invoiceService.GetInvoiceAsync(invoiceId, cancellationToken);
            if (invoice == null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found for e-Factura submission", invoiceId);
                return NotFound(new { message = "Invoice not found." });
            }

            _logger.LogInformation("Uploading XML to ANAF for invoice {InvoiceId}", invoiceId);

            var uploadInvoiceResult = await _anafIntegrationService.UploadXmlToAnaf(invoiceId, cancellationToken);

            _logger.LogInformation("E-Factura upload completed for invoice {InvoiceId}, success: {Success}",
                invoiceId, uploadInvoiceResult?.Success ?? false);

            return Ok(uploadInvoiceResult);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error uploading e-Factura for invoice {InvoiceId}",
                invoiceId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending e-Factura for invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { message = "An error occurred while sending the invoice to ANAF." });
        }
    }

    [HttpGet]
    [Route("GetAnafSubmissionStatus")]
    public async Task<IActionResult> GetAnafSubmissionStatus(Guid invoiceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetAnafSubmissionStatus request for invoice {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("GetAnafSubmissionStatus request with empty InvoiceId");
                return BadRequest(new { message = "InvoiceId is required" });
            }

            _logger.LogDebug("Retrieving ANAF submission status for invoice {InvoiceId}", invoiceId);

            var status = await _invoiceService.GetAnafSubmissionStatusAsync(invoiceId, cancellationToken);

            _logger.LogInformation("ANAF submission status retrieved for invoice {InvoiceId}: {Status}",
                invoiceId, status != null ? status.Status.ToString() : "unknown");

            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error retrieving ANAF submission status for invoice {InvoiceId}",
                invoiceId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ANAF submission status for invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { message = "An error occurred while retrieving the ANAF submission status." });
        }
    }

    [HttpGet]
    [Route("DownloadAnafResponse")]
    public async Task<IActionResult> DownloadAnafResponse(Guid invoiceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DownloadAnafResponse request for invoice {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("DownloadAnafResponse request with empty InvoiceId");
                return BadRequest(new { message = "InvoiceId is required" });
            }

            _logger.LogDebug("Downloading ANAF response for invoice {InvoiceId}", invoiceId);

            var downloadResponse = await _invoiceService.DownloadAnafResponseAsync(invoiceId, cancellationToken);

            var invoice = await _invoiceService.GetInvoiceAsync(invoiceId, cancellationToken);
            var fileName = invoice != null
                ? $"ANAF_Response_{invoice.Series}_{invoice.Number}.zip"
                : $"ANAF_Response_{invoiceId}.zip";

            _logger.LogInformation("ANAF response downloaded for invoice {InvoiceId}, size: {Size} bytes, fileName: {FileName}",
                invoiceId, downloadResponse?.ZipContent?.Length ?? 0, fileName);

            return File(downloadResponse.ZipContent, "application/zip", fileName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error downloading ANAF response for invoice {InvoiceId}",
                invoiceId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading ANAF response for invoice {InvoiceId}", invoiceId);
            return StatusCode(500, new { message = "An error occurred while downloading the ANAF response." });
        }
    }

    [HttpPost]
    [Route("CreateCreditNote")]
    public async Task<IActionResult> CreateCreditNote([FromBody] CreateCreditNoteRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CreateCreditNote request for original invoice {OriginalInvoiceId}",
            request?.OriginalInvoiceId);

        try
        {
            if (request == null)
            {
                _logger.LogWarning("CreateCreditNote request with null request body");
                return BadRequest(new { message = "Request body is required" });
            }

            if (request.OriginalInvoiceId == Guid.Empty)
            {
                _logger.LogWarning("CreateCreditNote request with empty OriginalInvoiceId");
                return BadRequest(new { message = "OriginalInvoiceId is required" });
            }

            _logger.LogDebug("Creating credit note for invoice {OriginalInvoiceId}", request.OriginalInvoiceId);

            var creditNote = await _invoiceService.CreateCreditNoteAsync(request, cancellationToken);

            _logger.LogInformation("Credit note successfully created - CreditNoteId: {CreditNoteId}, Series: {Series} Nr. {Number}, OriginalInvoiceId: {OriginalInvoiceId}",
                creditNote.Id, creditNote.Series, creditNote.Number, creditNote.OriginalInvoiceId);

            return Ok(creditNote);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error creating credit note for invoice {OriginalInvoiceId}",
                request?.OriginalInvoiceId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating credit note for invoice {OriginalInvoiceId}",
                request?.OriginalInvoiceId);
            return StatusCode(500, new { message = "An error occurred while creating the credit note." });
        }
    }
}

using EasyBilling.ANAFIntegration.EFactura.Interfaces;
using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EasyBilling.Application.Services;

public class InvoiceService(
    IInvoiceRepository invoiceRepository,
    ICompanyService companyService,
    IClientRepository clientRepository,
    IEFacturaXmlGenerator eFacturaXmlGenerator,
    IInvoiceAnafSubmissionRepository anafSubmissionRepository,
    IEFacturaService eFacturaService,
    ILogger<InvoiceService> logger) : IInvoiceService
{
    private readonly IInvoiceRepository _invoiceRepository = invoiceRepository;
    private readonly ICompanyService _companyService = companyService;
    private readonly IClientRepository _clientRepository = clientRepository;
    private readonly IEFacturaXmlGenerator _eFacturaXmlGenerator = eFacturaXmlGenerator;
    private readonly IInvoiceAnafSubmissionRepository _anafSubmissionRepository = anafSubmissionRepository;
    private readonly IEFacturaService _eFacturaService = eFacturaService;
    private readonly ILogger<InvoiceService> _logger = logger;

    private const int MaxInvoiceLines = 5;

    public async Task<InvoiceResponseDto> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating invoice for company {CompanyId}, series: {Series}, number: {Number}",
            request.CompanyId, request.Series, request.Number);

        try
        {
            var companyId = request.CompanyId;

            _logger.LogDebug("Validating company {CompanyId} exists", companyId);

            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                _logger.LogWarning("Company {CompanyId} not found", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            _logger.LogDebug("Company found: {CompanyName}", company.Name);

            if (request.InvoiceLines == null || request.InvoiceLines.Count == 0)
            {
                _logger.LogWarning("Invoice creation attempted with no invoice lines");
                throw new InvalidOperationException("Invoice must have at least one line.");
            }

            if (request.InvoiceLines.Count > MaxInvoiceLines)
            {
                _logger.LogWarning("Invoice creation attempted with {LineCount} lines (max: {MaxLines})",
                    request.InvoiceLines.Count, MaxInvoiceLines);
                throw new InvalidOperationException($"Invoice cannot have more than {MaxInvoiceLines} lines.");
            }

            _logger.LogDebug("Invoice has {LineCount} lines, validating client", request.InvoiceLines.Count);

            ClientResponseDto clientDto;
            Guid clientId;

            var clientCui = request.ClientCui ?? request.ClientDetails?.Cui;

            if (request.ClientId.HasValue)
            {
                _logger.LogDebug("Using existing client {ClientId}", request.ClientId.Value);

                var existingClient = await _clientRepository.GetByIdAsync(request.ClientId.Value);
                if (existingClient == null)
                {
                    _logger.LogWarning("Client {ClientId} not found", request.ClientId.Value);
                    throw new InvalidOperationException($"Client with ID '{request.ClientId}' does not exist.");
                }

                clientId = existingClient.Id;
                clientDto = MapClientToDto(existingClient);
                _logger.LogDebug("Client retrieved: {ClientName}", existingClient.Name);
            }
            else if (!string.IsNullOrWhiteSpace(clientCui))
            {
                var cleanCui = clientCui.Replace("RO", "").Replace(" ", "").Trim();

                _logger.LogDebug("Looking up client by CUI: {CleanCUI}", cleanCui);

                var existingClient = await _clientRepository.GetByCuiAndCompanyIdAsync(cleanCui, companyId, cancellationToken);

                if (existingClient != null)
                {
                    clientId = existingClient.Id;
                    clientDto = MapClientToDto(existingClient);
                    _logger.LogInformation("Client found in database: {ClientName} (CUI: {CUI})", existingClient.Name, cleanCui);
                }
                else
                {
                    _logger.LogDebug("Client not found in database, fetching from ANAF for CUI: {CUI}", cleanCui);

                    var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

                    if (anafDetails == null)
                    {
                        _logger.LogWarning("Client with CUI '{CUI}' not found in ANAF or database", cleanCui);
                        throw new InvalidOperationException($"Client with CUI '{cleanCui}' not found in database or ANAF.");
                    }

                    _logger.LogInformation("Client found in ANAF: {ClientName} (CUI: {CUI})", anafDetails.Name, cleanCui);

                    var newClient = new Client
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        Name = anafDetails.Name,
                        CUI = cleanCui,
                        Address = anafDetails.RegisteredAddress?.FormattedAddress,
                        County = anafDetails.RegisteredAddress?.County,
                        RegNumber = anafDetails.RegistrationNumber
                    };

                    await _clientRepository.AddAsync(newClient, cancellationToken);
                    clientId = newClient.Id;
                    clientDto = MapClientToDto(newClient);
                    _logger.LogInformation("New client created: {ClientId} - {ClientName}", newClient.Id, newClient.Name);
                }
            }
            else
            {
                _logger.LogWarning("Invoice creation attempted without ClientId, ClientCui, or ClientDetails");
                throw new InvalidOperationException("Either ClientId, ClientCui, or ClientDetails with CUI must be provided.");
            }

            decimal totalAmount = 0;
            decimal totalVat = 0;

            foreach (var line in request.InvoiceLines)
            {
                var vatRate = line.VatRate > 0 ? line.VatRate : (line.Vat ?? 0);
                var lineTotal = line.Quantity * line.UnitPrice;
                var lineVat = lineTotal * vatRate / 100;
                totalAmount += lineTotal;
                totalVat += lineVat;
            }

            _logger.LogDebug("Invoice totals calculated - Amount: {Amount}, VAT: {VAT}, Grand Total: {GrandTotal}",
                totalAmount, totalVat, totalAmount + totalVat);

            if (string.IsNullOrWhiteSpace(request.Series))
            {
                _logger.LogWarning("Invoice creation attempted without series");
                throw new InvalidOperationException("Invoice series is required.");
            }

            if (request.Number <= 0)
            {
                _logger.LogWarning("Invoice creation attempted with invalid number: {Number}", request.Number);
                throw new InvalidOperationException("Invoice number must be greater than 0.");
            }

            _logger.LogDebug("Checking last invoice number for series: {Series}", request.Series);

            var lastInvoiceWithSeries = await _invoiceRepository.GetLastInvoiceBySeriesAsync(companyId, request.Series, cancellationToken);

            if (lastInvoiceWithSeries != null)
            {
                if (request.Number <= lastInvoiceWithSeries.Number)
                {
                    _logger.LogWarning("Invoice number {RequestNumber} is not greater than last invoice {LastNumber} for series {Series}",
                        request.Number, lastInvoiceWithSeries.Number, request.Series);
                    throw new InvalidOperationException($"Invoice number must be greater than {lastInvoiceWithSeries.Number} for series '{request.Series}'.");
                }

                _logger.LogDebug("Last invoice for series {Series} has number {Number}", request.Series, lastInvoiceWithSeries.Number);
            }

            var invoiceId = Guid.NewGuid();
            var invoiceDate = request.Date ?? request.IssueDate ?? DateTime.UtcNow;

            if (invoiceDate.Kind == DateTimeKind.Unspecified)
            {
                invoiceDate = DateTime.SpecifyKind(invoiceDate, DateTimeKind.Utc);
            }
            else if (invoiceDate.Kind == DateTimeKind.Local)
            {
                invoiceDate = invoiceDate.ToUniversalTime();
            }

            DateTime? dueDate = null;
            if (request.DueDate.HasValue)
            {
                dueDate = request.DueDate.Value;
                if (dueDate.Value.Kind == DateTimeKind.Unspecified)
                {
                    dueDate = DateTime.SpecifyKind(dueDate.Value, DateTimeKind.Utc);
                }
                else if (dueDate.Value.Kind == DateTimeKind.Local)
                {
                    dueDate = dueDate.Value.ToUniversalTime();
                }
            }

            var invoice = new Invoice
            {
                Id = invoiceId,
                Date = invoiceDate,
                DueDate = dueDate,
                Series = request.Series,
                Number = request.Number,
                TotalAmount = totalAmount,
                Vat = totalVat,
                CompanyId = companyId,
                ClientId = clientId,
                InvoiceLines = request.InvoiceLines.Select(line =>
                {
                    var vatRate = line.VatRate > 0 ? line.VatRate : (line.Vat ?? 0);
                    return new InvoiceLine
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = invoiceId,
                        Description = line.Description,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        VatRate = vatRate,
                        Unit = line.Unit ?? "buc"
                    };
                }).ToList()
            };

            await _invoiceRepository.AddAsync(invoice, cancellationToken);

            _logger.LogInformation("Invoice successfully created: {InvoiceId} - Series: {Series} Nr. {Number}, Total: {Total}",
                invoice.Id, invoice.Series, invoice.Number, invoice.TotalAmount + invoice.Vat);

            return new InvoiceResponseDto
            {
                Id = invoice.Id,
                Date = invoice.Date,
                DueDate = invoice.DueDate,
                Series = invoice.Series,
                Number = invoice.Number,
                TotalAmount = totalAmount,
                TotalVat = totalVat,
                GrandTotal = totalAmount + totalVat,
                Company = new CompanyResponseDto
                {
                    Id = company.Id,
                    Name = company.Name,
                    CUI = company.CUI,
                    Address = company.Address,
                    County = company.County,
                    RegNumber = company.RegNumber,
                    IBAN = company.IBAN,
                    Bank = company.Bank
                },
                Client = clientDto,
                InvoiceLines = invoice.InvoiceLines!.Select(line => new InvoiceLineResponseDto
                {
                    Id = line.Id,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate,
                    Unit = line.Unit
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invoice for company {CompanyId}, series: {Series}, number: {Number}",
                request.CompanyId, request.Series, request.Number);
            throw;
        }
    }

    public async Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving invoice {InvoiceId}", invoiceId);

        try
        {
            if (invoiceId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve invoice with empty InvoiceId");
                return null;
            }

            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken);

            if (invoice == null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found", invoiceId);
                return null;
            }

            _logger.LogDebug("Invoice retrieved: {Series} Nr. {Number}, Company: {CompanyId}, Client: {ClientId}",
                invoice.Series, invoice.Number, invoice.CompanyId, invoice.ClientId);

            return invoice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoice {InvoiceId}", invoiceId);
            throw;
        }
    }

    public async Task<InvoiceResponseDto> GetInvoiceByIdAsync(Guid invoiceId, Guid companyId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving invoice {InvoiceId} for company {CompanyId}", invoiceId, companyId);

        try
        {
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken);

            if (invoice == null)
            {
                _logger.LogWarning("Invoice {InvoiceId} not found", invoiceId);
                throw new InvalidOperationException($"Invoice with ID '{invoiceId}' does not exist.");
            }

            if (invoice.CompanyId != companyId)
            {
                _logger.LogWarning("Invoice {InvoiceId} does not belong to company {CompanyId}, belongs to {ActualCompanyId}",
                    invoiceId, companyId, invoice.CompanyId);
                throw new InvalidOperationException("Invoice does not belong to the specified company.");
            }

            _logger.LogDebug("Invoice retrieved: {Series} Nr. {Number}", invoice.Series, invoice.Number);
            return MapInvoiceToDto(invoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoice {InvoiceId} for company {CompanyId}", invoiceId, companyId);
            throw;
        }
    }

    public async Task<List<InvoiceResponseDto>> GetInvoicesByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all invoices for company {CompanyId}", companyId);

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                _logger.LogWarning("Company {CompanyId} not found", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var invoices = await _invoiceRepository.GetAllByCompanyIdAsync(companyId, cancellationToken);

            _logger.LogInformation("Retrieved {InvoiceCount} invoices for company {CompanyId}",
                invoices.Count, companyId);

            return invoices.Select(MapInvoiceToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoices for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<PaginatedResult<InvoiceResponseDto>> GetInvoicesByCompanyIdPaginatedAsync(
        Guid companyId,
        InvoicePaginationFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving paginated invoices for company {CompanyId} - Page: {PageNumber}, PageSize: {PageSize}",
            companyId, filter.PageNumber, filter.PageSize);

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                _logger.LogWarning("Company {CompanyId} not found", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            _logger.LogDebug("Applying filters - SearchTerm: {SearchTerm}, SortBy: {SortBy}",
                filter.SearchTerm ?? "none", filter.SortBy ?? "none");

            var paginatedInvoices = await _invoiceRepository.GetInvoicesByCompanyIdPaginatedAsync(
                companyId,
                filter,
                cancellationToken);

            _logger.LogInformation("Retrieved {ItemCount} invoices out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                paginatedInvoices.Items.Count, paginatedInvoices.TotalCount, companyId, paginatedInvoices.PageNumber);

            return new PaginatedResult<InvoiceResponseDto>
            {
                Items = paginatedInvoices.Items.Select(MapInvoiceToDto).ToList(),
                PageNumber = paginatedInvoices.PageNumber,
                PageSize = paginatedInvoices.PageSize,
                TotalCount = paginatedInvoices.TotalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated invoices for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<InvoiceResponseDto>> GetCreditNotesByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all credit notes for company {CompanyId}", companyId);

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                _logger.LogWarning("Company {CompanyId} not found", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var creditNotes = await _invoiceRepository.GetAllCreditNotesByCompanyIdAsync(companyId, cancellationToken);

            _logger.LogInformation("Retrieved {CreditNoteCount} credit notes for company {CompanyId}",
                creditNotes.Count, companyId);

            return creditNotes.Select(MapInvoiceToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving credit notes for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<PaginatedResult<InvoiceResponseDto>> GetCreditNotesByCompanyIdPaginatedAsync(
        Guid companyId,
        InvoicePaginationFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving paginated credit notes for company {CompanyId} - Page: {PageNumber}, PageSize: {PageSize}",
            companyId, filter.PageNumber, filter.PageSize);

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                _logger.LogWarning("Company {CompanyId} not found", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            _logger.LogDebug("Applying filters - SearchTerm: {SearchTerm}, SortBy: {SortBy}",
                filter.SearchTerm ?? "none", filter.SortBy ?? "none");

            var paginatedCreditNotes = await _invoiceRepository.GetCreditNotesByCompanyIdPaginatedAsync(
                companyId,
                filter,
                cancellationToken);

            _logger.LogInformation("Retrieved {ItemCount} credit notes out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                paginatedCreditNotes.Items.Count, paginatedCreditNotes.TotalCount, companyId, paginatedCreditNotes.PageNumber);

            return new PaginatedResult<InvoiceResponseDto>
            {
                Items = paginatedCreditNotes.Items.Select(MapInvoiceToDto).ToList(),
                PageNumber = paginatedCreditNotes.PageNumber,
                PageSize = paginatedCreditNotes.PageSize,
                TotalCount = paginatedCreditNotes.TotalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated credit notes for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<LastInvoiceNumberDto> GetLastInvoiceNumberAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving last invoice number for company {CompanyId}", companyId);

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                _logger.LogWarning("Company {CompanyId} not found", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var lastInvoice = await _invoiceRepository.GetLastInvoiceByCompanyIdAsync(companyId, cancellationToken);

            var lastInvoiceNumber = lastInvoice?.Number ?? 1;
            var lastInvoiceSeries = lastInvoice?.Series ?? "A";

            _logger.LogDebug("Last invoice for company {CompanyId}: Series={Series}, Number={Number}",
                companyId, lastInvoiceSeries, lastInvoiceNumber);

            return new LastInvoiceNumberDto
            {
                Series = lastInvoiceSeries,
                Number = lastInvoiceNumber
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last invoice number for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating PDF for invoice {InvoiceId}", invoiceId);

        try
        {
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken)
                ?? throw new InvalidOperationException("Invoice not found.");

            _logger.LogDebug("Invoice loaded: {Series} Nr. {Number}, Total: {Total}",
                invoice.Series, invoice.Number, invoice.TotalAmount + invoice.Vat);

            QuestPDF.Settings.License = LicenseType.Community;
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Column(col =>
                    {
                        col.Spacing(15);

                        col.Item().Text("FACTURA")
                            .FontSize(24)
                            .Bold()
                            .FontColor(Colors.Blue.Medium);

                        decimal grandTotal = 0;
                        var vatRates = invoice.InvoiceLines?.Select(l => l.VatRate).Distinct().ToList() ?? new List<decimal>();
                        var vatRateDisplay = vatRates.Count == 1 ? $"{vatRates[0]}%" : "Diverse";

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.Span("Seria ");
                                text.Span(invoice.Series);
                                text.Span(" Nr. ");
                                text.Span(invoice.Number.ToString());
                                text.Span(" din ");
                                text.Span(invoice.Date.ToString("dd.MM.yyyy"));
                            });

                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.Span("Cota TVA ");
                                text.Span(vatRateDisplay);
                            });
                        });

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Furnizor").Bold().FontColor(Colors.Blue.Medium);
                                c.Item().Text(invoice.Company.Name).Bold();
                                c.Item().Text($"CIF: {invoice.Company.CUI}");
                                c.Item().Text($"Reg. com.: {invoice.Company.RegNumber}");
                                c.Item().Text($"Adresa: {invoice.Company.Address}");
                                c.Item().Text($"Judet: {invoice.Company.County}");
                                c.Item().Text($"IBAN(RON): {invoice.Company.IBAN}");
                                c.Item().Text($"Banca: {invoice.Company.Bank}");
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Client").Bold().FontColor(Colors.Blue.Medium);
                                c.Item().Text(invoice.Client.Name).Bold();
                                c.Item().Text($"CIF: {invoice.Client.CUI}");
                                c.Item().Text($"Reg. com.: {invoice.Client.RegNumber}");
                                c.Item().Text($"Adresa: {invoice.Client.Address}");
                                c.Item().Text($"Judet: {invoice.Client.County}");
                            });
                        });

                        col.Item().Text("Produse/servicii")
                            .Bold()
                            .FontColor(Colors.Blue.Medium);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25);
                                columns.RelativeColumn();
                                columns.ConstantColumn(40);
                                columns.ConstantColumn(45);
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(80);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("#").Bold();
                                header.Cell().Text("Denumire").Bold();
                                header.Cell().AlignCenter().Text("U.M.").Bold();
                                header.Cell().AlignCenter().Text("Cant.").Bold();
                                header.Cell().AlignRight().Text("Pret fara TVA").Bold();
                                header.Cell().AlignRight().Text("Valoare").Bold();
                                header.Cell().AlignRight().Text("Valoare TVA").Bold();
                            });

                            var index = 1;
                            decimal total = 0;
                            decimal totalVat = 0;
                            foreach (var item in invoice.InvoiceLines!)
                            {
                                total += item.Quantity * item.UnitPrice;
                                totalVat += (item.Quantity * item.UnitPrice) * item.VatRate / 100;

                                var lineTotal = item.Quantity * item.UnitPrice;
                                var lineVat = lineTotal * item.VatRate / 100;
                                table.Cell().Text(index.ToString());
                                table.Cell().Text(item.Description);
                                table.Cell().AlignCenter().Text(item.Unit);
                                table.Cell().AlignCenter().Text(item.Quantity.ToString("0.##"));
                                table.Cell().AlignRight().Text(item.UnitPrice.ToString("0.00"));
                                table.Cell().AlignRight().Text(lineTotal.ToString("0.00"));
                                table.Cell().AlignRight().Text(lineVat.ToString("0.00"));
                                index++;
                            }

                            table.Cell().ColumnSpan(4).Text("Total").Bold();
                            table.Cell().AlignRight().Text(total.ToString("0.00")).Bold();
                            table.Cell().AlignRight().Text(total.ToString("0.00")).Bold();
                            table.Cell().AlignRight().Text(totalVat.ToString("0.00")).Bold();

                            grandTotal += total + totalVat;
                        });
                        col.Item().AlignRight().Text($"Total factura: {grandTotal:0.00} Lei")
                            .FontSize(14)
                            .Bold();

                        col.Item().Column(c =>
                        {
                            c.Spacing(3);
                            c.Item().Text(text =>
                            {
                                text.Span("Intocmit de: ");
                                text.Span("");
                            });

                            c.Item().LineHorizontal(0.5f);

                            c.Item().Text("Factura este valabila fara semnatura si stampila, conform art. 319 alin. 29 din Codul Fiscal.");
                            c.Item().Text("Emis cu EasyBilling, program de facturare").FontSize(10);
                        });
                    });
                });
            }).GeneratePdf();

            _logger.LogInformation("PDF generated successfully for invoice {InvoiceId}, size: {Size} bytes",
                invoiceId, pdfBytes.Length);

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF for invoice {InvoiceId}", invoiceId);
            throw;
        }
    }

    public async Task<string> GenerateXmlForAnaf(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating XML for ANAF submission for invoice {InvoiceId}", invoiceId);

        try
        {
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken)
                ?? throw new InvalidOperationException("Invoice not found.");

            _logger.LogDebug("Invoice loaded: {Series} Nr. {Number}", invoice.Series, invoice.Number);

            var xmlContent = _eFacturaXmlGenerator.GenerateXml(invoice);

            _logger.LogInformation("XML generated successfully for invoice {InvoiceId}, size: {Size} bytes",
                invoiceId, xmlContent.Length);

            return xmlContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate XML for ANAF for invoice {InvoiceId}", invoiceId);
            throw new InvalidOperationException("Failed to generate XML for ANAF.", ex);
        }
    }

    private static ClientResponseDto MapClientToDto(Client client)
    {
        return new ClientResponseDto
        {
            Id = client.Id,
            Name = client.Name,
            CUI = client.CUI,
            Address = client.Address,
            County = client.County,
            RegNumber = client.RegNumber,
            IBAN = client.IBAN,
            Bank = client.Bank
        };
    }

    private static InvoiceResponseDto MapInvoiceToDto(Invoice invoice)
    {
        var totalAmount = invoice.InvoiceLines?.Sum(l => l.Quantity * l.UnitPrice) ?? 0;
        var totalVat = invoice.InvoiceLines?.Sum(l => l.Quantity * l.UnitPrice * l.VatRate / 100) ?? 0;

        return new InvoiceResponseDto
        {
            Id = invoice.Id,
            Date = invoice.Date,
            DueDate = invoice.DueDate,
            Series = invoice.Series,
            Number = invoice.Number,
            TotalAmount = totalAmount,
            TotalVat = totalVat,
            GrandTotal = totalAmount + totalVat,
            Type = invoice.Type,
            OriginalInvoiceId = invoice.OriginalInvoiceId,
            OriginalInvoiceNumber = invoice.OriginalInvoice != null
                ? $"{invoice.OriginalInvoice.Series} nr. {invoice.OriginalInvoice.Number}"
                : null,
            Company = new CompanyResponseDto
            {
                Id = invoice.Company.Id,
                Name = invoice.Company.Name,
                CUI = invoice.Company.CUI,
                Address = invoice.Company.Address,
                County = invoice.Company.County,
                RegNumber = invoice.Company.RegNumber,
                IBAN = invoice.Company.IBAN,
                Bank = invoice.Company.Bank
            },
            Client = MapClientToDto(invoice.Client),
            InvoiceLines = invoice.InvoiceLines?.Select(line => new InvoiceLineResponseDto
            {
                Id = line.Id,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                VatRate = line.VatRate,
                Unit = line.Unit
            }).ToList() ?? new List<InvoiceLineResponseDto>()
        };
    }

    public async Task<AnafSubmissionStatusDto> GetAnafSubmissionStatusAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving ANAF submission status for invoice {InvoiceId}", invoiceId);

        try
        {
            var latestSubmission = await _anafSubmissionRepository.GetLatestByInvoiceIdAsync(invoiceId, cancellationToken);

            if (latestSubmission == null)
            {
                _logger.LogInformation("No ANAF submission found for invoice {InvoiceId}, returning pending status",
                    invoiceId);

                return new AnafSubmissionStatusDto
                {
                    Id = null,
                    Status = AnafSubmissionStatus.Pending,
                    ErrorMessage = null,
                    UploadedAt = null,
                    LastCheckedAt = null,
                    DownloadId = null
                };
            }

            _logger.LogInformation("ANAF submission status retrieved for invoice {InvoiceId}: {Status}",
                invoiceId, latestSubmission.Status);

            return new AnafSubmissionStatusDto
            {
                Id = latestSubmission.Id,
                Status = latestSubmission.Status,
                ErrorMessage = latestSubmission.ErrorMessage,
                UploadedAt = latestSubmission.UploadedAt,
                LastCheckedAt = latestSubmission.LastCheckedAt,
                DownloadId = latestSubmission.DownloadId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ANAF submission status for invoice {InvoiceId}", invoiceId);
            throw;
        }
    }

    public async Task<EFacturaDownloadResponse> DownloadAnafResponseAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading ANAF response for invoice {InvoiceId}", invoiceId);

        try
        {
            var successfulSubmission = await _anafSubmissionRepository.GetSuccessfulByInvoiceIdAsync(invoiceId, cancellationToken);

            if (successfulSubmission == null)
            {
                _logger.LogWarning("No successful ANAF submission found for invoice {InvoiceId}", invoiceId);
                throw new InvalidOperationException("No successful ANAF submission found for this invoice.");
            }

            _logger.LogDebug("Successful submission found for invoice {InvoiceId}, SubmissionId: {SubmissionId}",
                invoiceId, successfulSubmission.Id);

            if (successfulSubmission.DownloadId == null)
            {
                _logger.LogWarning("No download ID available for successful submission {SubmissionId} of invoice {InvoiceId}",
                    successfulSubmission.Id, invoiceId);
                throw new InvalidOperationException("No download ID available for the successful ANAF submission.");
            }

            _logger.LogDebug("Attempting to download ANAF response using downloadId: {DownloadId}",
                successfulSubmission.DownloadId);

            var downloadResponse = await _eFacturaService.DownloadAnafSignedInvoiceAsync(
                invoiceId,
                successfulSubmission.Invoice.CompanyId,
                successfulSubmission.DownloadId,
                cancellationToken: cancellationToken);

            if (downloadResponse == null || !downloadResponse.Success)
            {
                _logger.LogWarning("Failed to download ANAF response for invoice {InvoiceId}, Success: {Success}",
                    invoiceId, downloadResponse?.Success);
                throw new InvalidOperationException("Failed to download ANAF response.");
            }

            if (downloadResponse.ZipContent == null || downloadResponse.ZipContent.Length == 0)
            {
                _logger.LogWarning("Downloaded ANAF response is empty for invoice {InvoiceId}", invoiceId);
                throw new InvalidOperationException("Downloaded ANAF response is empty.");
            }

            if (downloadResponse.ErrorMessage != null)
            {
                _logger.LogWarning("Error in downloaded ANAF response for invoice {InvoiceId}: {ErrorMessage}",
                    invoiceId, downloadResponse.ErrorMessage);
                throw new InvalidOperationException($"Error in downloaded ANAF response: {downloadResponse.ErrorMessage}");
            }

            _logger.LogInformation("ANAF response downloaded successfully for invoice {InvoiceId}, size: {Size} bytes",
                invoiceId, downloadResponse.ZipContent.Length);

            return downloadResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading ANAF response for invoice {InvoiceId}", invoiceId);
            throw new InvalidOperationException("Failed to download ANAF response.", ex);
        }
    }

    public async Task<InvoiceResponseDto> CreateCreditNoteAsync(CreateCreditNoteRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating credit note for original invoice {OriginalInvoiceId}, series: {Series}",
            request.OriginalInvoiceId, request.Series);

        try
        {
            var originalInvoice = await _invoiceRepository.GetByIdWithDetailsAsync(request.OriginalInvoiceId, cancellationToken);

            if (originalInvoice == null)
            {
                _logger.LogWarning("Original invoice {OriginalInvoiceId} not found", request.OriginalInvoiceId);
                throw new InvalidOperationException($"Original invoice with ID '{request.OriginalInvoiceId}' does not exist.");
            }

            _logger.LogDebug("Original invoice found: {Series} Nr. {Number}", originalInvoice.Series, originalInvoice.Number);

            if (originalInvoice.IsCreditNote)
            {
                _logger.LogWarning("Cannot create credit note for another credit note {OriginalInvoiceId}",
                    request.OriginalInvoiceId);
                throw new InvalidOperationException("Cannot create a credit note for another credit note.");
            }

            var series = request.Series ?? originalInvoice.Series;

            var nextNumber = await _invoiceRepository.GetLastInvoiceBySeriesAsync(originalInvoice.CompanyId, series, cancellationToken);
            var number = request.Number != null && int.TryParse(request.Number, out var parsedNumber)
                ? parsedNumber
                : (nextNumber != null ? nextNumber.Number + 1 : originalInvoice.Number + 1);

            _logger.LogDebug("Credit note will be created with series: {Series}, number: {Number}", series, number);

            List<InvoiceLine> lines;

            if (request.Lines != null && request.Lines.Any())
            {
                _logger.LogDebug("Using custom lines ({LineCount}) for credit note", request.Lines.Count);

                lines = request.Lines.Select(line => new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = Guid.NewGuid(), // Will be set when creating the credit note
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate > 0 ? line.VatRate : 0,
                    Unit = line.Unit ?? "buc"
                }).ToList();
            }
            else
            {
                _logger.LogDebug("Using original invoice lines ({LineCount}) for credit note", originalInvoice.InvoiceLines.Count);

                lines = originalInvoice.InvoiceLines.Select(l => new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = Guid.NewGuid(), // Will be set when creating the credit note
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    Unit = l.Unit
                }).ToList();
            }

            decimal totalAmount = 0;
            decimal totalVat = 0;

            foreach (var line in lines)
            {
                var vatRate = line.VatRate;
                var lineTotal = line.Quantity * line.UnitPrice;
                var lineVat = lineTotal * vatRate / 100;
                totalAmount += lineTotal;
                totalVat += lineVat;
            }

            _logger.LogDebug("Credit note totals calculated - Amount: {Amount}, VAT: {VAT}",
                totalAmount, totalVat);

            var creditNote = new Invoice
            {
                Id = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                Type = InvoiceType.CreditNote,
                Series = series,
                Number = number,
                TotalAmount = totalAmount,
                Vat = totalVat,
                CompanyId = originalInvoice.CompanyId,
                ClientId = originalInvoice.ClientId,
                OriginalInvoiceId = originalInvoice.Id,
                InvoiceLines = lines
            };

            await _invoiceRepository.AddAsync(creditNote, cancellationToken);

            _logger.LogInformation("Credit note successfully created: {CreditNoteId} - Series: {Series} Nr. {Number}",
                creditNote.Id, creditNote.Series, creditNote.Number);

            var result = await _invoiceRepository.GetByIdWithDetailsAsync(creditNote.Id, cancellationToken);

            if (result == null)
            {
                _logger.LogError("Failed to retrieve created credit note {CreditNoteId}", creditNote.Id);
                throw new InvalidOperationException("Failed to retrieve the created credit note.");
            }

            return MapInvoiceToDto(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating credit note for original invoice {OriginalInvoiceId}",
                request.OriginalInvoiceId);
            throw;
        }
    }
}

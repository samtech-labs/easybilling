using EasyBilling.ANAFIntegration.EFactura.Interfaces;
using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Application.Responses;
using EasyBilling.Domain.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EasyBilling.Application.Services
{
    public class InvoiceService(
        IInvoiceRepository invoiceRepository,
        ICompanyService companyService,
        IClientRepository clientRepository,
        IEFacturaXmlGenerator eFacturaXmlGenerator,
        IInvoiceAnafSubmissionRepository anafSubmissionRepository,
        IEFacturaService eFacturaService) : IInvoiceService
    {
        private readonly IInvoiceRepository _invoiceRepository = invoiceRepository;
        private readonly ICompanyService _companyService = companyService;
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly IEFacturaXmlGenerator _eFacturaXmlGenerator = eFacturaXmlGenerator;
        private readonly IInvoiceAnafSubmissionRepository _anafSubmissionRepository = anafSubmissionRepository;
        private readonly IEFacturaService _eFacturaService = eFacturaService;

        private const int MaxInvoiceLines = 5;

        public async Task<InvoiceResponseDto> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
        {
            var companyId = request.CompanyId;

            // Validate company exists
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            // Validate invoice lines
            if (request.InvoiceLines == null || request.InvoiceLines.Count == 0)
            {
                throw new InvalidOperationException("Invoice must have at least one line.");
            }

            if (request.InvoiceLines.Count > MaxInvoiceLines)
            {
                throw new InvalidOperationException($"Invoice cannot have more than {MaxInvoiceLines} lines.");
            }

            // Get or create client data
            ClientResponseDto clientDto;
            Guid clientId;

            // Determine client CUI from either ClientCui or ClientDetails
            var clientCui = request.ClientCui ?? request.ClientDetails?.Cui;

            if (request.ClientId.HasValue)
            {
                // Client exists in database
                var existingClient = await _clientRepository.GetByIdAsync(request.ClientId.Value);
                if (existingClient == null)
                {
                    throw new InvalidOperationException($"Client with ID '{request.ClientId}' does not exist.");
                }

                clientId = existingClient.Id;
                clientDto = MapClientToDto(existingClient);
            }
            else if (!string.IsNullOrWhiteSpace(clientCui))
            {
                // Try to find client by CUI in database first
                var cleanCui = clientCui.Replace("RO", "").Replace(" ", "").Trim();
                var existingClient = await _clientRepository.GetByCuiAndCompanyIdAsync(cleanCui, companyId, cancellationToken);

                if (existingClient != null)
                {
                    clientId = existingClient.Id;
                    clientDto = MapClientToDto(existingClient);
                }
                else
                {
                    // Client not in database, fetch from ANAF
                    var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

                    if (anafDetails == null)
                    {
                        throw new InvalidOperationException($"Client with CUI '{cleanCui}' not found in database or ANAF.");
                    }

                    // Create a new client from ANAF data
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
                }
            }
            else
            {
                throw new InvalidOperationException("Either ClientId, ClientCui, or ClientDetails with CUI must be provided.");
            }

            // Calculate totals (use Vat if VatRate is 0, for frontend compatibility)
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

            // Validate series and number
            if (string.IsNullOrWhiteSpace(request.Series))
            {
                throw new InvalidOperationException("Invoice series is required.");
            }

            if (request.Number <= 0)
            {
                throw new InvalidOperationException("Invoice number must be greater than 0.");
            }

            // Check if there's a previous invoice with the same series
            var lastInvoiceWithSeries = await _invoiceRepository.GetLastInvoiceBySeriesAsync(companyId, request.Series, cancellationToken);

            if (lastInvoiceWithSeries != null)
            {
                // Ensure the new number is greater than the last one for this series
                if (request.Number <= lastInvoiceWithSeries.Number)
                {
                    throw new InvalidOperationException($"Invoice number must be greater than {lastInvoiceWithSeries.Number} for series '{request.Series}'.");
                }
            }

            // Create invoice
            var invoiceId = Guid.NewGuid();
            var invoiceDate = request.Date ?? request.IssueDate ?? DateTime.UtcNow;

            // Ensure DateTime is UTC for PostgreSQL compatibility
            if (invoiceDate.Kind == DateTimeKind.Unspecified)
            {
                invoiceDate = DateTime.SpecifyKind(invoiceDate, DateTimeKind.Utc);
            }
            else if (invoiceDate.Kind == DateTimeKind.Local)
            {
                invoiceDate = invoiceDate.ToUniversalTime();
            }

            // Handle DueDate if provided
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

            // Build response
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

        public async Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            return await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken);
        }

        public async Task<InvoiceResponseDto> GetInvoiceByIdAsync(Guid invoiceId, Guid companyId, CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken);

            if (invoice == null)
            {
                throw new InvalidOperationException($"Invoice with ID '{invoiceId}' does not exist.");
            }

            if (invoice.CompanyId != companyId)
            {
                throw new InvalidOperationException("Invoice does not belong to the specified company.");
            }

            return MapInvoiceToDto(invoice);
        }

        public async Task<List<InvoiceResponseDto>> GetInvoicesByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var invoices = await _invoiceRepository.GetAllByCompanyIdAsync(companyId, cancellationToken);

            return invoices.Select(MapInvoiceToDto).ToList();
        }

        public async Task<LastInvoiceNumberDto> GetLastInvoiceNumberAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var lastInvoice = await _invoiceRepository.GetLastInvoiceByCompanyIdAsync(companyId, cancellationToken);

            var lastInvoiceNumber = lastInvoice?.Number ?? 1;
            var lastInvoiceSeries = lastInvoice?.Series ?? "A";

            return new LastInvoiceNumberDto
            {
                Series = lastInvoiceSeries,
                Number = lastInvoiceNumber
            };
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken)
                ?? throw new InvalidOperationException("Invoice not found.");

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
                        // Get VAT rate from invoice lines (show if all lines have the same rate)
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

            return pdfBytes;
        }

        public async Task<string> GenerateXmlForAnaf(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId, cancellationToken)
                ?? throw new InvalidOperationException("Invoice not found.");
            try
            {
                var xmlContent = _eFacturaXmlGenerator.GenerateXml(invoice);
                return xmlContent;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate XML for ANAF.", ex);
            };
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
            var latestSubmission = await _anafSubmissionRepository.GetLatestByInvoiceIdAsync(invoiceId, cancellationToken);

            if (latestSubmission == null)
            {
                // No submission exists yet - return a default status
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

        public async Task<EFacturaDownloadResponse> DownloadAnafResponseAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var successfulSubmission = await _anafSubmissionRepository.GetSuccessfulByInvoiceIdAsync(invoiceId, cancellationToken);

            if (successfulSubmission == null)
            {
                throw new InvalidOperationException("No successful ANAF submission found for this invoice.");
            }

            if (successfulSubmission.DownloadId == null)
            {
                throw new InvalidOperationException("No download ID available for the successful ANAF submission.");
            }

            try
            {
                var downloadResponse = await _eFacturaService.DownloadAnafSignedInvoiceAsync(invoiceId, successfulSubmission.Invoice.CompanyId, successfulSubmission.DownloadId, cancellationToken: cancellationToken);

                if (downloadResponse == null || !downloadResponse.Success)
                {
                    throw new InvalidOperationException("Failed to download ANAF response.");
                }

                if (downloadResponse.ZipContent == null || downloadResponse.ZipContent.Length == 0)
                {
                    throw new InvalidOperationException("Downloaded ANAF response is empty.");
                }

                if (downloadResponse.ErrorMessage != null)
                {
                    throw new InvalidOperationException($"Error in downloaded ANAF response: {downloadResponse.ErrorMessage}");
                }

                return downloadResponse;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to download ANAF response.", ex);
            }
        }

        public async Task<PagedResponse<InvoiceResponseDto>> GetPagedInvoicesByCompanyAsync(Guid companyId, PageRequest page,
            CancellationToken cancellationToken = default)
        {
            var invoicePage = await _invoiceRepository.GetInvoicesPagedAsync(
                companyId, page.Page, page.PageSize, cancellationToken);

            return new PagedResponse<InvoiceResponseDto>
            {
                Items = invoicePage.items.Select(MapInvoiceToDto).ToList(),
                Page = page.Page,
                PageSize = page.PageSize,
                TotalCount = invoicePage.totalCount
            };
        }
    }
}

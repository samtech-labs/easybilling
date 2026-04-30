using EasyBilling.ANAFIntegration.EFactura.Interfaces;
using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Repositories;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EasyBilling.Application.Services
{
    public class InvoiceService(
        IInvoiceRepository invoiceRepository,
        ICompanyService companyService,
        IUserService userService,
        IClientRepository clientRepository,
        IBankAccountRepository bankAccountRepository,
        IEFacturaXmlGenerator eFacturaXmlGenerator,
        IInvoiceAnafSubmissionRepository anafSubmissionRepository,
        IEFacturaService eFacturaService) : IInvoiceService
    {
        private readonly IInvoiceRepository _invoiceRepository = invoiceRepository;
        private readonly ICompanyService _companyService = companyService;
        private readonly IUserService _userService = userService;
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly IBankAccountRepository _bankAccountRepository = bankAccountRepository;
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

            // Validate bank account (if provided) belongs to the same company
            BankAccount? bankAccount = null;
            if (request.BankAccountId.HasValue)
            {
                bankAccount = await _bankAccountRepository.GetByIdAsync(request.BankAccountId.Value, cancellationToken)
                    ?? throw new InvalidOperationException($"Bank account with ID '{request.BankAccountId}' does not exist.");

                if (bankAccount.CompanyId != companyId)
                {
                    throw new InvalidOperationException("Bank account does not belong to the specified company.");
                }
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
                Currency = request.Currency,
                CompanyId = companyId,
                ClientId = clientId,
                BankAccountId = bankAccount?.Id,
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
                        Unit = UnitOfMeasure.NormalizeOrDefault(line.Unit)
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
                Currency = invoice.Currency,
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
                BankAccountId = bankAccount?.Id,
                BankAccountBankName = bankAccount?.BankName,
                BankAccountIban = bankAccount?.Iban,
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

        public async Task<PaginatedResult<InvoiceResponseDto>> GetInvoicesByCompanyIdPaginatedAsync(
            Guid companyId,
            InvoicePaginationFilter filter,
            CancellationToken cancellationToken = default)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var paginatedInvoices = await _invoiceRepository.GetInvoicesByCompanyIdPaginatedAsync(
                companyId,
                filter,
                cancellationToken);

            return new PaginatedResult<InvoiceResponseDto>
            {
                Items = paginatedInvoices.Items.Select(MapInvoiceToDto).ToList(),
                PageNumber = paginatedInvoices.PageNumber,
                PageSize = paginatedInvoices.PageSize,
                TotalCount = paginatedInvoices.TotalCount
            };
        }

        public async Task<List<InvoiceResponseDto>> GetCreditNotesByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var creditNotes = await _invoiceRepository.GetAllCreditNotesByCompanyIdAsync(companyId, cancellationToken);

            return creditNotes.Select(MapInvoiceToDto).ToList();
        }

        public async Task<PaginatedResult<InvoiceResponseDto>> GetCreditNotesByCompanyIdPaginatedAsync(
            Guid companyId,
            InvoicePaginationFilter filter,
            CancellationToken cancellationToken = default)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId, cancellationToken);
            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var paginatedCreditNotes = await _invoiceRepository.GetCreditNotesByCompanyIdPaginatedAsync(
                companyId,
                filter,
                cancellationToken);

            return new PaginatedResult<InvoiceResponseDto>
            {
                Items = paginatedCreditNotes.Items.Select(MapInvoiceToDto).ToList(),
                PageNumber = paginatedCreditNotes.PageNumber,
                PageSize = paginatedCreditNotes.PageSize,
                TotalCount = paginatedCreditNotes.TotalCount
            };
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

            // --- Color palette (blue/slate) ---
            var primaryColor = "#2563EB";   // Blue-600 — clean, professional blue
            var primaryDark = "#1E40AF";    // Blue-800 — for text emphasis
            var primaryLight = "#EFF6FF";   // Blue-50 — very subtle tint for backgrounds
            var darkText = "#1E293B";       // Slate-800
            var mutedText = "#64748B";      // Slate-500
            var lightBg = "#F8FAFC";        // Slate-50 — alternating row
            var borderColor = "#E2E8F0";    // Slate-200
            var white = "#FFFFFF";

            // --- Number formatting (Romanian standard: space as thousand separator) ---
            var roNumberFormat = new System.Globalization.NumberFormatInfo
            {
                NumberDecimalSeparator = ".",
                NumberGroupSeparator = " ",
                NumberGroupSizes = new[] { 3 }
            };

            string FormatAmount(decimal amount) => amount.ToString("#,##0.00", roNumberFormat);

            // --- Calculations ---
            var lines = invoice.InvoiceLines?.ToList() ?? new List<InvoiceLine>();
            var vatRates = lines.Select(l => l.VatRate).Distinct().ToList();
            var vatRateDisplay = vatRates.Count == 1 ? $"{vatRates[0]}%" : "Diverse";
            var currencyLabel = GetCurrencyLabel(invoice.Currency);

            decimal subtotal = lines.Sum(l => l.Quantity * l.UnitPrice);
            decimal totalVat = lines.Sum(l => l.Quantity * l.UnitPrice * l.VatRate / 100);
            decimal grandTotal = subtotal + totalVat;

            // Bank details: prefer the invoice's selected bank account, fall back to legacy Company fields
            var bankIban = invoice.BankAccount?.Iban ?? invoice.Company.IBAN;
            var bankName = invoice.BankAccount?.BankName ?? invoice.Company.Bank;
            var bankIbanDisplay = FormatIbanForDisplay(bankIban);
            var ibanLabel = invoice.BankAccount != null
                ? $"IBAN ({GetCurrencyLabel(invoice.BankAccount.Currency)}):"
                : "IBAN:";

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.MarginHorizontal(40);
                    page.MarginVertical(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontColor(darkText));

                    // ===== HEADER =====
                    page.Header().Column(header =>
                    {
                        // Accent bar at very top
                        header.Item().Height(4).Background(primaryColor);

                        header.Item().PaddingTop(18).Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("FACTURA")
                                    .FontSize(28)
                                    .Bold()
                                    .FontColor(primaryDark)
                                    .LetterSpacing(0.08f);

                                col.Item().PaddingTop(6).Text(text =>
                                {
                                    text.Span("Seria ").FontSize(10).FontColor(mutedText);
                                    text.Span(invoice.Series).FontSize(10).Bold();
                                    text.Span("  Nr. ").FontSize(10).FontColor(mutedText);
                                    text.Span(invoice.Number.ToString()).FontSize(10).Bold();
                                    text.Span("  din ").FontSize(10).FontColor(mutedText);
                                    text.Span(invoice.Date.ToString("dd.MM.yyyy")).FontSize(10).Bold();
                                });
                            });

                            row.ConstantItem(180).AlignRight().AlignMiddle().Text(text =>
                            {
                                text.Span("Cota TVA ").FontSize(10).FontColor(mutedText);
                                text.Span(vatRateDisplay).FontSize(10).Bold().FontColor(primaryDark);
                            });
                        });

                        // Divider line below header
                        header.Item().PaddingTop(12).LineHorizontal(1.5f).LineColor(primaryColor);
                    });

                    // ===== CONTENT =====
                    page.Content().PaddingTop(18).Column(col =>
                    {
                        // --- Supplier / Client boxes ---
                        col.Item().Row(row =>
                        {
                            // Supplier box
                            row.RelativeItem().BorderLeft(3).BorderColor(primaryColor)
                                .Background(primaryLight).Padding(14).Column(c =>
                                {
                                    c.Item().PaddingBottom(8).Text("FURNIZOR")
                                        .FontSize(8)
                                        .Bold()
                                        .FontColor(primaryColor)
                                        .LetterSpacing(0.12f);

                                    c.Item().Text(invoice.Company.Name)
                                        .FontSize(12)
                                        .Bold();

                                    c.Item().PaddingTop(8).Table(t =>
                                    {
                                        t.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(72);
                                            columns.RelativeColumn();
                                        });

                                        AddDetailRow(t, "CIF:", invoice.Company.CUI, mutedText);
                                        AddDetailRow(t, "Reg. com.:", invoice.Company.RegNumber, mutedText);
                                        AddDetailRow(t, "Adresa:", invoice.Company.Address, mutedText);
                                        AddDetailRow(t, "Județ:", invoice.Company.County, mutedText);
                                        AddDetailRow(t, ibanLabel, bankIbanDisplay, mutedText);
                                        AddDetailRow(t, "Banca:", bankName, mutedText);
                                    });
                                });

                            row.ConstantItem(16); // Spacer

                            // Client box
                            row.RelativeItem().BorderLeft(3).BorderColor(borderColor)
                                .Background(lightBg).Padding(14).Column(c =>
                                {
                                    c.Item().PaddingBottom(8).Text("CLIENT")
                                        .FontSize(8)
                                        .Bold()
                                        .FontColor(primaryColor)
                                        .LetterSpacing(0.12f);

                                    c.Item().Text(invoice.Client.Name)
                                        .FontSize(12)
                                        .Bold();

                                    c.Item().PaddingTop(8).Table(t =>
                                    {
                                        t.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(72);
                                            columns.RelativeColumn();
                                        });

                                        AddDetailRow(t, "CIF:", invoice.Client.CUI, mutedText);
                                        AddDetailRow(t, "Reg. com.:", invoice.Client.RegNumber, mutedText);
                                        AddDetailRow(t, "Adresa:", invoice.Client.Address, mutedText);
                                        AddDetailRow(t, "Județ:", invoice.Client.County, mutedText);
                                    });
                                });
                        });

                        col.Item().Height(22); // Spacing

                        // --- Products/Services section header ---
                        col.Item().PaddingBottom(10).Text("PRODUSE / SERVICII")
                            .FontSize(8)
                            .Bold()
                            .FontColor(primaryColor)
                            .LetterSpacing(0.12f);

                        // --- Table ---
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(28);    // #
                                columns.RelativeColumn(3);     // Denumire
                                columns.ConstantColumn(45);    // U.M.
                                columns.ConstantColumn(50);    // Cant.
                                columns.ConstantColumn(85);    // Preț fără TVA
                                columns.ConstantColumn(85);    // Valoare
                                columns.ConstantColumn(85);    // Valoare TVA
                            });

                            // Table header — clean style with bottom border
                            table.Header(header =>
                            {
                                var headerStyle = TextStyle.Default
                                    .FontSize(8)
                                    .Bold()
                                    .FontColor(darkText);

                                var currencyStyle = TextStyle.Default
                                    .FontSize(7)
                                    .FontColor(primaryColor);

                                // Main header row
                                header.Cell().BorderBottom(1.5f).BorderColor(primaryColor).Padding(6)
                                    .Text("#").Style(headerStyle);
                                header.Cell().BorderBottom(1.5f).BorderColor(primaryColor).Padding(6)
                                    .Text("Denumire").Style(headerStyle);
                                header.Cell().BorderBottom(1.5f).BorderColor(primaryColor).Padding(6).AlignCenter()
                                    .Text("U.M.").Style(headerStyle);
                                header.Cell().BorderBottom(1.5f).BorderColor(primaryColor).Padding(6).AlignCenter()
                                    .Text("Cant.").Style(headerStyle);
                                header.Cell().BorderBottom(1.5f).BorderColor(primaryColor).Padding(6).AlignRight()
                                    .Column(c =>
                                    {
                                        c.Item().AlignRight().Text("Preț fără TVA").Style(headerStyle);
                                        c.Item().AlignRight().Text(currencyLabel).Style(currencyStyle);
                                    });
                                header.Cell().BorderBottom(1.5f).BorderColor(primaryColor).Padding(6).AlignRight()
                                    .Column(c =>
                                    {
                                        c.Item().AlignRight().Text("Valoare").Style(headerStyle);
                                        c.Item().AlignRight().Text(currencyLabel).Style(currencyStyle);
                                    });
                                header.Cell().BorderBottom(1.5f).BorderColor(primaryColor).Padding(6).AlignRight()
                                    .Column(c =>
                                    {
                                        c.Item().AlignRight().Text("Valoare TVA").Style(headerStyle);
                                        c.Item().AlignRight().Text(currencyLabel).Style(currencyStyle);
                                    });
                            });

                            // Table rows with subtle alternating backgrounds
                            var index = 1;
                            foreach (var item in lines)
                            {
                                var lineTotal = item.Quantity * item.UnitPrice;
                                var lineVat = lineTotal * item.VatRate / 100;
                                var rowBg = index % 2 == 0 ? lightBg : white;

                                var cellStyle = TextStyle.Default.FontSize(9);

                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(borderColor)
                                    .Padding(6).Text(index.ToString()).Style(cellStyle);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(borderColor)
                                    .Padding(6).Text(item.Description).Style(cellStyle);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(borderColor)
                                    .Padding(6).AlignCenter().Text(item.Unit).Style(cellStyle);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(borderColor)
                                    .Padding(6).AlignCenter().Text(item.Quantity.ToString("0.##")).Style(cellStyle);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(borderColor)
                                    .Padding(6).AlignRight().Text(FormatAmount(item.UnitPrice)).Style(cellStyle);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(borderColor)
                                    .Padding(6).AlignRight().Text(FormatAmount(lineTotal)).Style(cellStyle);
                                table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(borderColor)
                                    .Padding(6).AlignRight().Text(FormatAmount(lineVat)).Style(cellStyle);

                                index++;
                            }

                            // Total row
                            var totalStyle = TextStyle.Default.FontSize(9).Bold();

                            table.Cell().ColumnSpan(4)
                                .BorderTop(1).BorderColor(primaryColor)
                                .PaddingVertical(8).PaddingHorizontal(6).AlignRight()
                                .Text("Total").Style(totalStyle);
                            table.Cell()
                                .BorderTop(1).BorderColor(primaryColor)
                                .PaddingVertical(8).PaddingHorizontal(6).AlignRight()
                                .Text(FormatAmount(subtotal)).Style(totalStyle);
                            table.Cell()
                                .BorderTop(1).BorderColor(primaryColor)
                                .PaddingVertical(8).PaddingHorizontal(6).AlignRight()
                                .Text(FormatAmount(subtotal)).Style(totalStyle);
                            table.Cell()
                                .BorderTop(1).BorderColor(primaryColor)
                                .PaddingVertical(8).PaddingHorizontal(6).AlignRight()
                                .Text(FormatAmount(totalVat)).Style(totalStyle);
                        });

                        col.Item().Height(12);

                        // --- Grand total box ---
                        col.Item().Row(grandRow =>
                        {
                            // Întocmit de on the left
                            grandRow.RelativeItem().AlignBottom().PaddingBottom(4).Text(text =>
                            {
                                text.Span("Întocmit de: ").FontSize(9).FontColor(mutedText);
                                text.Span(""); // name if available
                            });

                            // Total factură box on the right
                            grandRow.ConstantItem(280)
                                .Border(1.5f).BorderColor(primaryColor)
                                .Padding(10)
                                .Row(r =>
                                {
                                    r.RelativeItem().AlignMiddle().Text("Total factură")
                                        .FontSize(11)
                                        .Bold()
                                        .FontColor(darkText);
                                    r.AutoItem().AlignMiddle().AlignRight()
                                        .Text(FormatAmount(grandTotal) + " " + currencyLabel)
                                        .FontSize(14)
                                        .Bold()
                                        .FontColor(primaryDark);
                                });
                        });
                    });

                    // ===== FOOTER =====
                    page.Footer().Column(footer =>
                    {
                        footer.Item().LineHorizontal(1.5f).LineColor(primaryColor);

                        footer.Item().PaddingTop(10).Row(row =>
                        {
                            var footerStyle = TextStyle.Default.FontSize(7).FontColor(mutedText);

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(invoice.Company.Name).Style(footerStyle).Bold();
                                c.Item().Text($"Capital social: 200 RON").Style(footerStyle);
                            });

                            row.RelativeItem().AlignCenter().Column(c =>
                            {
                                c.Item().Text($"{ibanLabel} {bankIbanDisplay}").Style(footerStyle);
                                c.Item().Text($"Banca: {bankName}").Style(footerStyle);
                            });

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("Factura este valabilă fără semnătură și ștampilă,")
                                    .Style(footerStyle);
                                c.Item().Text("conform art. 319 alin. 29 din Codul Fiscal.")
                                    .Style(footerStyle);
                            });
                        });

                        footer.Item().PaddingTop(8).AlignCenter()
                            .Text("Emis cu EasyBilling — easybilling.ro")
                            .FontSize(7)
                            .FontColor(primaryColor)
                            .Bold();

                        // Bottom accent bar
                        footer.Item().PaddingTop(6).Height(3).Background(primaryColor);
                    });
                });
            }).GeneratePdf();

            return pdfBytes;
        }

        private static string? FormatIbanForDisplay(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban))
                return iban;

            var compact = iban.Replace(" ", "").Trim();
            return string.Join(" ", Enumerable.Range(0, (compact.Length + 3) / 4)
                .Select(i => compact.Substring(i * 4, Math.Min(4, compact.Length - i * 4))));
        }

        // Helper method — add to the same class
        private static void AddDetailRow(TableDescriptor table, string label, string? value, string mutedColor)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            table.Cell().PaddingVertical(2)
                .Text(label)
                .FontSize(8)
                .FontColor(mutedColor);

            table.Cell().PaddingVertical(2)
                .Text(value)
                .FontSize(9)
                .Bold();
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
                Type = invoice.Type,
                Currency = invoice.Currency,
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
                BankAccountId = invoice.BankAccountId,
                BankAccountBankName = invoice.BankAccount?.BankName,
                BankAccountIban = invoice.BankAccount?.Iban,
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

        private static string GetCurrencyLabel(Currency currency) => currency switch
        {
            Currency.EUR => "EUR",
            _ => "RON"
        };

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

        public async Task<InvoiceResponseDto> CreateCreditNoteAsync(CreateCreditNoteRequest request, CancellationToken cancellationToken = default)
        {
            var originalInvoice = await _invoiceRepository.GetByIdWithDetailsAsync(request.OriginalInvoiceId, cancellationToken);

            if (originalInvoice == null)
            {
                throw new InvalidOperationException($"Original invoice with ID '{request.OriginalInvoiceId}' does not exist.");
            }

            if (originalInvoice.IsCreditNote)
            {
                throw new InvalidOperationException("Cannot create a credit note for another credit note.");
            }

            var series = request.Series ?? originalInvoice.Series;

            var nextNumber = await _invoiceRepository.GetLastInvoiceBySeriesAsync(originalInvoice.CompanyId, series, cancellationToken);
            var number = request.Number != null && int.TryParse(request.Number, out var parsedNumber)
                ? parsedNumber
                : (nextNumber != null ? nextNumber.Number + 1 : originalInvoice.Number + 1);

            List<InvoiceLine> lines;

            if (request.Lines != null && request.Lines.Any())
            {
                lines = request.Lines.Select(line => new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = Guid.NewGuid(), // Will be set when creating the credit note
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate > 0 ? line.VatRate : 0,
                    Unit = UnitOfMeasure.NormalizeOrDefault(line.Unit)
                }).ToList();
            }
            else
            {
                lines = originalInvoice.InvoiceLines.Select(l => new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = Guid.NewGuid(), // Will be set when creating the credit note
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    Unit = UnitOfMeasure.NormalizeOrDefault(l.Unit)
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

            var creditNote = new Invoice
            {
                Id = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                Type = InvoiceType.CreditNote,
                Currency = originalInvoice.Currency,
                Series = series,
                Number = number,
                TotalAmount = totalAmount,
                Vat = totalVat,
                CompanyId = originalInvoice.CompanyId,
                ClientId = originalInvoice.ClientId,
                BankAccountId = originalInvoice.BankAccountId,
                OriginalInvoiceId = originalInvoice.Id,
                InvoiceLines = lines
            };

            await _invoiceRepository.AddAsync(creditNote, cancellationToken);

            var result = await _invoiceRepository.GetByIdWithDetailsAsync(creditNote.Id, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to retrieve the created credit note.");
            }

            return MapInvoiceToDto(result);
        }

        public async Task<List<Invoice>> GetAllForUserByPeriodAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var user = await _userService.GetUserByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
            }

            return await _invoiceRepository.GetAllForUserByPeriodAsync(userId, startDate, endDate, cancellationToken);
        }
    }
}
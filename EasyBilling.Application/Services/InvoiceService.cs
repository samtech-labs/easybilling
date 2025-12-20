using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EasyBilling.Application.Services
{
    public class InvoiceService(
        IInvoiceRepository invoiceRepository,
        ICompanyService companyService,
        IClientRepository clientRepository) : IInvoiceService
    {
        private readonly IInvoiceRepository _invoiceRepository = invoiceRepository;
        private readonly ICompanyService _companyService = companyService;
        private readonly IClientRepository _clientRepository = clientRepository;

        private const int MaxInvoiceLines = 5;

        public async Task<InvoiceResponseDto> CreateInvoiceAsync(CreateInvoiceRequest request, Guid companyId)
        {
            // Validate company exists
            var company = await _companyService.GetCompanyByIdAsync(companyId);
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
            else if (!string.IsNullOrWhiteSpace(request.ClientCui))
            {
                // Try to find client by CUI in database first
                var cleanCui = request.ClientCui.Replace("RO", "").Replace(" ", "").Trim();
                var existingClient = await _clientRepository.GetByCuiAndCompanyIdAsync(cleanCui, companyId);

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

                    await _clientRepository.AddAsync(newClient);
                    clientId = newClient.Id;
                    clientDto = MapClientToDto(newClient);
                }
            }
            else
            {
                throw new InvalidOperationException("Either ClientId or ClientCui must be provided.");
            }

            // Calculate totals
            decimal totalAmount = 0;
            decimal totalVat = 0;

            foreach (var line in request.InvoiceLines)
            {
                var lineTotal = line.Quantity * line.UnitPrice;
                var lineVat = lineTotal * line.VatRate / 100;
                totalAmount += lineTotal;
                totalVat += lineVat;
            }

            // Create invoice
            var invoiceId = Guid.NewGuid();
            var invoice = new Invoice
            {
                Id = invoiceId,
                Date = request.Date ?? DateTime.UtcNow,
                Series = request.Series,
                Number = request.Number,
                TotalAmount = totalAmount,
                Vat = totalVat,
                CompanyId = companyId,
                ClientId = clientId,
                InvoiceLines = request.InvoiceLines.Select(line => new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate,
                    Unit = line.Unit
                }).ToList()
            };

            await _invoiceRepository.AddAsync(invoice);

            // Build response
            return new InvoiceResponseDto
            {
                Id = invoice.Id,
                Date = invoice.Date,
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

        public async Task<InvoiceResponseDto> GetInvoiceByIdAsync(Guid invoiceId, Guid companyId)
        {
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId);

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

        public async Task<List<InvoiceResponseDto>> GetInvoicesByCompanyIdAsync(Guid companyId)
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);
            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            var invoices = await _invoiceRepository.GetAllByCompanyIdAsync(companyId);

            return invoices.Select(MapInvoiceToDto).ToList();
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(Guid invoiceId)
        {
            var invoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoiceId)
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
                                text.Span(invoice.Vat.ToString());
                                text.Span("%");
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
    }
}

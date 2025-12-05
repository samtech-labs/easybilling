using EasyBilling.Application.Interfaces;
using EasyBilling.Application.IServices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EasyBilling.Application.Services
{
    public class InvoiceService(ICompanyRepository companyRepository) : IInvoiceService
    {
        public readonly ICompanyRepository _companyRepository = companyRepository;
        public async Task<byte[]> CreateInvoiceAsync(Guid companyId)
        {
            var company = await _companyRepository.GetByIdAsync(companyId);

            if (company is null)
            {
                throw new Exception("Company not found");
            }

            // TODO: The invoice model should be extended, having a dedicated model, and invoice related classes.
            // For the purpose of this example, we will use hardcoded data.
            // After that, we will integrate with real data from the database and also move this logic in a dedicated class.

            QuestPDF.Settings.License = LicenseType.Community;

            var invoice = new
            {
                Series = "AB",
                Number = "123",
                Date = new DateTime(2025, 1, 15),
                VatRate = "19%",
                VatLabel = "taxare normala",
                Company = new
                {
                    Name = "SC Exemplu SRL",
                    Cui = "RO12345678",
                    ReNumber = "J00/1234/2020",
                    Address = "Str. Exemplu 10, Bucuresti",
                    County = "Bucuresti",
                    Iban = "RO49AAAA1B31007593840000",
                    Bank = "Banca Exemplu",
                    FooterLine1 = "SC Exemplu SRL, capital social 200 RON",
                    FooterLine2 = "Punct de lucru: Str. Test 5, Bucuresti"
                },
                Client = new
                {
                    Name = "Client Demo SRL",
                    Cif = "RO87654321",
                    RegCom = "J00/4321/2021",
                    Address = "Str. Clientului 20, Cluj-Napoca",
                    County = "Cluj",
                    Country = "Romania"
                },
                Items = new[]
                {
                    new { Name = "Servicii programare", Unit = "h", Quantity = 10m, PriceWithoutVat = 150m, Value = 1500m, VatValue = 285m },
                    new { Name = "Consultanta tehnica", Unit = "h", Quantity = 5m, PriceWithoutVat = 200m, Value = 1000m, VatValue = 190m }
                },
                Totals = new
                {
                    PriceWithoutVat = 1500m + 1000m,
                    Value = 1500m + 1000m,
                    VatValue = 285m + 190m,
                    GrandTotal = 1500m + 1000m + 285m + 190m
                },
                PreparedByName = "Ion Popescu",
                Footer = new
                {
                    LegalText = "Factura este valabila fara semnatura si stampila, conform art. 319 alin. 29 din Codul Fiscal.",
                    SoftwarePrefix = "Emis cu",
                    SoftwareName = "MyInvoiceApp",
                    SoftwareSuffix = "program de facturare",
                    DocumentCode = "INV-001"
                }
            };

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

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.Span("Seria ");
                                text.Span(invoice.Series);
                                text.Span(" Nr. ");
                                text.Span(invoice.Number);
                                text.Span(" din ");
                                text.Span(invoice.Date.ToString("dd.MM.yyyy"));
                            });

                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.Span("Cota TVA ");
                                text.Span(invoice.VatRate);
                                text.Span(" ");
                                text.Span(invoice.VatLabel);
                            });
                        });

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Furnizor").Bold().FontColor(Colors.Blue.Medium);
                                c.Item().Text(invoice.Company.Name).Bold();
                                c.Item().Text($"CIF: {invoice.Company.Cui}");
                                c.Item().Text($"Reg. com.: {invoice.Company.ReNumber}");
                                c.Item().Text($"Adresa: {invoice.Company.Address}");
                                c.Item().Text($"Judet: {invoice.Company.County}");
                                c.Item().Text($"IBAN(RON): {invoice.Company.Iban}");
                                c.Item().Text($"Banca: {invoice.Company.Bank}");
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Client").Bold().FontColor(Colors.Blue.Medium);
                                c.Item().Text(invoice.Client.Name).Bold();
                                c.Item().Text($"CIF: {invoice.Client.Cif}");
                                c.Item().Text($"Reg. com.: {invoice.Client.RegCom}");
                                c.Item().Text($"Adresa: {invoice.Client.Address}");
                                c.Item().Text($"Judet: {invoice.Client.County}");
                                c.Item().Text($"Tara: {invoice.Client.Country}");
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
                            foreach (var item in invoice.Items)
                            {
                                table.Cell().Text(index.ToString());
                                table.Cell().Text(item.Name);
                                table.Cell().AlignCenter().Text(item.Unit);
                                table.Cell().AlignCenter().Text(item.Quantity.ToString("0.##"));
                                table.Cell().AlignRight().Text(item.PriceWithoutVat.ToString("0.00"));
                                table.Cell().AlignRight().Text(item.Value.ToString("0.00"));
                                table.Cell().AlignRight().Text(item.VatValue.ToString("0.00"));
                                index++;
                            }

                            table.Cell().ColumnSpan(4).Text("Total").Bold();
                            table.Cell().AlignRight().Text(invoice.Totals.PriceWithoutVat.ToString("0.00")).Bold();
                            table.Cell().AlignRight().Text(invoice.Totals.Value.ToString("0.00")).Bold();
                            table.Cell().AlignRight().Text(invoice.Totals.VatValue.ToString("0.00")).Bold();
                        });

                        col.Item().AlignRight().Text($"Total factura: {invoice.Totals.GrandTotal:0.00} Lei")
                            .FontSize(14)
                            .Bold();

                        col.Item().Column(c =>
                        {
                            c.Spacing(3);
                            c.Item().Text(text =>
                            {
                                text.Span("Intocmit de: ");
                                text.Span(invoice.PreparedByName);
                            });

                            c.Item().LineHorizontal(0.5f);

                            c.Item().Text(invoice.Company.Name).Bold();
                            c.Item().Text(invoice.Company.FooterLine1);
                            c.Item().Text(invoice.Company.FooterLine2);
                            c.Item().Text(invoice.Footer.LegalText);
                            c.Item().Text(
                                $"{invoice.Footer.SoftwarePrefix} {invoice.Footer.SoftwareName}, " +
                                $"{invoice.Footer.SoftwareSuffix}   Cod document: {invoice.Footer.DocumentCode}"
                            ).FontSize(10);
                        });
                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("Pagina ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf();

            return pdfBytes;
        }
    }
}

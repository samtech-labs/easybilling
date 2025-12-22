using EasyBilling.Application.Interfaces;
using EasyBilling.Application.IServices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Azure.Storage.Blobs;

namespace EasyBilling.Application.Services
{
    public class InvoiceService(IInvoiceRepository invoiceRepository, BlobStorageService blobStorageService) : IInvoiceService
    {
        public readonly IInvoiceRepository _invoiceRepository = invoiceRepository;
        private readonly BlobStorageService _blobStorageService = blobStorageService;
        public async Task<byte[]> CreateInvoiceAsync(Guid invoiceId)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId) ?? throw new Exception("Invoice not found.");

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
                            foreach (var item in invoice.InvoiceLines)
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

            try
            {
                await _blobStorageService.UploadFileToBlob(invoiceId, pdfBytes);
            }
            catch (Azure.RequestFailedException ex)
            {
                throw new InvalidOperationException(
                    $"Blob upload failed (Status: {ex.Status}, Code: {ex.ErrorCode})",
                    ex);
            }

            return pdfBytes;
        }
    }
}

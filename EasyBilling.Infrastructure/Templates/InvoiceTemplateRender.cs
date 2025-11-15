using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;
using Microsoft.Extensions.Hosting;
using Scriban;

namespace EasyBilling.Infrastructure.Templates
{
    public class InvoiceTemplateRender(IHostEnvironment env) : IInvoiceTemplateRender
    {
        public readonly IHostEnvironment _env = env;

        // TODO: add invoice parameter to constructor
        public async Task<string> RenderAsync(Company company)
        {
            var templatePath = Path.Combine(_env.ContentRootPath, "static/templates", "invoice.html");
            var text = await File.ReadAllTextAsync(templatePath);

            var scribanTemplate = Template.Parse(text);

            var model = new
            {
                company = new
                {
                    name = company.Name,
                    cui = company.CUI,
                    regNumber = company.RegNumber,
                    address = company.Address,
                    iban = company.IBAN,
                    bank = company.Bank,
                    footerLine1 = "Capital social: 200; Tel.: +40765385066",
                    footerLine2 = ""
                },
                footer = new
                {
                    legalText = "Factura este valabila fara semnatura si stampila, conform art. 319 alin. 29 din legea 227/2015"
                },
                invoice = new
                {
                    series = "A",
                    number = "001",
                    date = DateTime.UtcNow.ToString("dd.MM.yyyy"),
                    vat_rate = "21%",
                    vat_label = "TVA"
                },
                client = new
                {
                    name = "Client SRL",
                    cui = "RO12345678",
                    regNumber = "J40/1234/2020",
                    address = "Str. Exemplu, Nr. 10, Bucuresti",
                },
                totals = new { },
                prepared_by = new { name = "Andrei Samoila" },
                item = new { }
            };

            return await scribanTemplate.RenderAsync(model, memberRenamer: member => member.Name);
        }
    }
}

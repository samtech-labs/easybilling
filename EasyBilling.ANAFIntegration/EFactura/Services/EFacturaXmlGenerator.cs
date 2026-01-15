using EasyBilling.ANAFIntegration.EFactura.Helpers;
using EasyBilling.ANAFIntegration.EFactura.Interfaces;
using EasyBilling.Domain.Enums;
using EasyBilling.Domain.Models;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace EasyBilling.ANAFIntegration.EFactura.Services;

public class EFacturaXmlGenerator : IEFacturaXmlGenerator
{
    private static readonly XNamespace NS_INVOICE = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace NS_CREDIT_NOTE = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    private static readonly XNamespace NS_CAC = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace NS_CBC = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";


    public string GenerateXml(Invoice invoice)
    {
        if (invoice.Company == null)
            throw new ArgumentException("Invoice must have Company information loaded");

        if (invoice.Client == null)
            throw new ArgumentException("Invoice must have Client information loaded");

        if (invoice.Type == InvoiceType.CreditNote && invoice.OriginalInvoice == null)
            throw new ArgumentException("Credit Note must have OriginalInvoice information loaded");

        XElement rootElement = invoice.Type == InvoiceType.CreditNote
            ? CreateCreditNoteElement(invoice)
            : CreateInvoiceElement(invoice);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            rootElement
        );

        return doc.ToString();
    }

    public void GenerateXmlFile(Invoice invoice, string filePath)
    {
        var xml = GenerateXml(invoice);
        File.WriteAllText(filePath, xml, Encoding.UTF8);
    }

    #region Invoice (380)

    private XElement CreateInvoiceElement(Invoice invoice)
    {
        var (lineExtensionTotal, taxTotal, taxGroups) = CalculateTotals(invoice);
        var dueDate = invoice.DueDate ?? invoice.Date;

        var elements = new List<object?>
        {
            new XAttribute(XNamespace.Xmlns + "cac", NS_CAC),
            new XAttribute(XNamespace.Xmlns + "cbc", NS_CBC),

            new XElement(NS_CBC + "UBLVersionID", "2.1"),
            new XElement(NS_CBC + "CustomizationID", "urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:CIUS-RO:1.0.1"),
            new XElement(NS_CBC + "ID", $"{invoice.Series} nr. {invoice.Number}"),
            new XElement(NS_CBC + "IssueDate", invoice.Date.ToString("yyyy-MM-dd")),
            new XElement(NS_CBC + "DueDate", dueDate.ToString("yyyy-MM-dd")),
            new XElement(NS_CBC + "InvoiceTypeCode", "380"),
            new XElement(NS_CBC + "DocumentCurrencyCode", "RON")
        };

        elements.Add(CreateSupplierParty(invoice.Company!));
        elements.Add(CreateCustomerParty(invoice.Client!));
        elements.Add(CreateTaxTotal(taxTotal, taxGroups));
        elements.Add(CreateLegalMonetaryTotal(lineExtensionTotal, taxTotal));
        elements.AddRange(CreateInvoiceLines(invoice.InvoiceLines));

        return new XElement(NS_INVOICE + "Invoice", elements.Where(e => e != null));
    }

    private IEnumerable<XElement> CreateInvoiceLines(ICollection<InvoiceLine>? lines)
    {
        if (lines == null || !lines.Any())
            yield break;

        var lineNumber = 1;
        foreach (var line in lines)
        {
            var lineTotal = line.Quantity * line.UnitPrice;

            yield return new XElement(NS_CAC + "InvoiceLine",
                new XElement(NS_CBC + "ID", lineNumber.ToString()),
                new XElement(NS_CBC + "InvoicedQuantity",
                    new XAttribute("unitCode", MapUnitCode(line.Unit)),
                    FormatQuantity(line.Quantity)),
                new XElement(NS_CBC + "LineExtensionAmount",
                    new XAttribute("currencyID", "RON"),
                    FormatDecimal(lineTotal)),
                new XElement(NS_CAC + "Item",
                    new XElement(NS_CBC + "Name", line.Description ?? String.Empty),
                    new XElement(NS_CAC + "ClassifiedTaxCategory",
                        new XElement(NS_CBC + "ID", GetTaxCategoryCode(line.VatRate)),
                        new XElement(NS_CBC + "Percent", FormatDecimal(line.VatRate)),
                        new XElement(NS_CAC + "TaxScheme",
                            new XElement(NS_CBC + "ID", "VAT")))),
                new XElement(NS_CAC + "Price",
                    new XElement(NS_CBC + "PriceAmount",
                        new XAttribute("currencyID", "RON"),
                        FormatDecimal(line.UnitPrice)))
            );

            lineNumber++;
        }
    }

    #endregion

    #region Credit Note (381)

    private XElement CreateCreditNoteElement(Invoice invoice)
    {
        var (lineExtensionTotal, taxTotal, taxGroups) = CalculateTotals(invoice);

        var elements = new List<object?>
        {
            new XAttribute(XNamespace.Xmlns + "cac", NS_CAC),
            new XAttribute(XNamespace.Xmlns + "cbc", NS_CBC),

            new XElement(NS_CBC + "UBLVersionID", "2.1"),
            new XElement(NS_CBC + "CustomizationID", "urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:CIUS-RO:1.0.1"),
            new XElement(NS_CBC + "ID", $"{invoice.Series} nr. {invoice.Number}"),
            new XElement(NS_CBC + "IssueDate", invoice.Date.ToString("yyyy-MM-dd")),
            new XElement(NS_CBC + "CreditNoteTypeCode", "381"),
            new XElement(NS_CBC + "DocumentCurrencyCode", "RON")
        };

        elements.Add(CreateBillingReference(invoice.OriginalInvoice!));
        elements.Add(CreateSupplierParty(invoice.Company!));
        elements.Add(CreateCustomerParty(invoice.Client!));
        elements.Add(CreateTaxTotal(taxTotal, taxGroups));
        elements.Add(CreateLegalMonetaryTotal(lineExtensionTotal, taxTotal));
        elements.AddRange(CreateCreditNoteLines(invoice.InvoiceLines));

        return new XElement(NS_CREDIT_NOTE + "CreditNote", elements.Where(e => e != null));
    }

    private XElement CreateBillingReference(Invoice originalInvoice)
    {
        return new XElement(NS_CAC + "BillingReference",
            new XElement(NS_CAC + "InvoiceDocumentReference",
                new XElement(NS_CBC + "ID", $"{originalInvoice.Series} nr. {originalInvoice.Number}"),
                new XElement(NS_CBC + "IssueDate", originalInvoice.Date.ToString("yyyy-MM-dd"))
            )
        );
    }

    private IEnumerable<XElement> CreateCreditNoteLines(ICollection<InvoiceLine>? lines)
    {
        if (lines == null || !lines.Any())
            yield break;

        var lineNumber = 1;
        foreach (var line in lines)
        {
            var lineTotal = line.Quantity * line.UnitPrice;

            yield return new XElement(NS_CAC + "CreditNoteLine",
                new XElement(NS_CBC + "ID", lineNumber.ToString()),
                new XElement(NS_CBC + "CreditedQuantity",
                    new XAttribute("unitCode", MapUnitCode(line.Unit)),
                    FormatQuantity(line.Quantity)),
                new XElement(NS_CBC + "LineExtensionAmount",
                    new XAttribute("currencyID", "RON"),
                    FormatDecimal(lineTotal)),
                new XElement(NS_CAC + "Item",
                    new XElement(NS_CBC + "Name", line.Description ?? String.Empty),
                    new XElement(NS_CAC + "ClassifiedTaxCategory",
                        new XElement(NS_CBC + "ID", GetTaxCategoryCode(line.VatRate)),
                        new XElement(NS_CBC + "Percent", FormatDecimal(line.VatRate)),
                        new XElement(NS_CAC + "TaxScheme",
                            new XElement(NS_CBC + "ID", "VAT")))),
                new XElement(NS_CAC + "Price",
                    new XElement(NS_CBC + "PriceAmount",
                        new XAttribute("currencyID", "RON"),
                        FormatDecimal(line.UnitPrice)))
            );

            lineNumber++;
        }
    }

    #endregion

    #region Common Elements

    private XElement CreateSupplierParty(Company company)
    {
        var cui = FormatCUI(company.CUI);
        var countyCode = LocationHelper.GetCountyCode(company.County);
        var city = LocationHelper.NormalizeCity(company.City);

        return new XElement(NS_CAC + "AccountingSupplierParty",
            new XElement(NS_CAC + "Party",
                new XElement(NS_CAC + "PostalAddress",
                    new XElement(NS_CBC + "StreetName", company.Address ?? String.Empty),
                    new XElement(NS_CBC + "CityName", city),
                    new XElement(NS_CBC + "CountrySubentity", countyCode),
                    new XElement(NS_CAC + "Country",
                        new XElement(NS_CBC + "IdentificationCode", "RO"))),
                new XElement(NS_CAC + "PartyTaxScheme",
                    new XElement(NS_CBC + "CompanyID", cui),
                    new XElement(NS_CAC + "TaxScheme",
                        new XElement(NS_CBC + "ID", "VAT"))),
                new XElement(NS_CAC + "PartyLegalEntity",
                    new XElement(NS_CBC + "RegistrationName", company.Name),
                    string.IsNullOrEmpty(company.RegNumber) ? null :
                        new XElement(NS_CBC + "CompanyID", company.RegNumber))
            )
        );
    }

    private XElement CreateCustomerParty(Client client)
    {
        var cui = FormatCUI(client.CUI);
        var countyCode = LocationHelper.GetCountyCode(client.County);
        var city = LocationHelper.NormalizeCity(client.City);

        return new XElement(NS_CAC + "AccountingCustomerParty",
            new XElement(NS_CAC + "Party",
                string.IsNullOrEmpty(client.RegNumber) ? null :
                    new XElement(NS_CAC + "PartyIdentification",
                        new XElement(NS_CBC + "ID", client.RegNumber)),
                new XElement(NS_CAC + "PostalAddress",
                    new XElement(NS_CBC + "StreetName", client.Address ?? String.Empty),
                    new XElement(NS_CBC + "CityName", city),
                    new XElement(NS_CBC + "CountrySubentity", countyCode),
                    new XElement(NS_CAC + "Country",
                        new XElement(NS_CBC + "IdentificationCode", "RO"))),
                new XElement(NS_CAC + "PartyTaxScheme",
                    new XElement(NS_CBC + "CompanyID", cui),
                    new XElement(NS_CAC + "TaxScheme",
                        new XElement(NS_CBC + "ID", "VAT"))),
                new XElement(NS_CAC + "PartyLegalEntity",
                    new XElement(NS_CBC + "RegistrationName", client.Name))
            )
        );
    }

    private XElement CreateTaxTotal(decimal totalTax, Dictionary<decimal, (decimal taxableAmount, decimal taxAmount)> taxGroups)
    {
        var taxSubtotals = taxGroups.Select(g =>
            new XElement(NS_CAC + "TaxSubtotal",
                new XElement(NS_CBC + "TaxableAmount",
                    new XAttribute("currencyID", "RON"),
                    FormatDecimal(g.Value.taxableAmount)),
                new XElement(NS_CBC + "TaxAmount",
                    new XAttribute("currencyID", "RON"),
                    FormatDecimal(g.Value.taxAmount)),
                new XElement(NS_CAC + "TaxCategory",
                    new XElement(NS_CBC + "ID", GetTaxCategoryCode(g.Key)),
                    new XElement(NS_CBC + "Percent", FormatDecimal(g.Key)),
                    new XElement(NS_CAC + "TaxScheme",
                        new XElement(NS_CBC + "ID", "VAT"))))
        );

        return new XElement(NS_CAC + "TaxTotal",
            new XElement(NS_CBC + "TaxAmount",
                new XAttribute("currencyID", "RON"),
                FormatDecimal(totalTax)),
            taxSubtotals
        );
    }

    private XElement CreateLegalMonetaryTotal(decimal lineExtensionTotal, decimal taxTotal)
    {
        var totalWithTax = lineExtensionTotal + taxTotal;

        return new XElement(NS_CAC + "LegalMonetaryTotal",
            new XElement(NS_CBC + "LineExtensionAmount",
                new XAttribute("currencyID", "RON"),
                FormatDecimal(lineExtensionTotal)),
            new XElement(NS_CBC + "TaxExclusiveAmount",
                new XAttribute("currencyID", "RON"),
                FormatDecimal(lineExtensionTotal)),
            new XElement(NS_CBC + "TaxInclusiveAmount",
                new XAttribute("currencyID", "RON"),
                FormatDecimal(totalWithTax)),
            new XElement(NS_CBC + "PayableAmount",
                new XAttribute("currencyID", "RON"),
                FormatDecimal(totalWithTax))
        );
    }

    private (decimal lineExtensionTotal, decimal taxTotal, Dictionary<decimal, (decimal taxableAmount, decimal taxAmount)> taxGroups)
        CalculateTotals(Invoice invoice)
    {
        decimal lineExtensionTotal = 0;
        var taxGroups = new Dictionary<decimal, (decimal taxableAmount, decimal taxAmount)>();

        if (invoice.InvoiceLines != null)
        {
            foreach (var line in invoice.InvoiceLines)
            {
                var lineTotal = line.Quantity * line.UnitPrice;
                lineExtensionTotal += lineTotal;

                if (!taxGroups.ContainsKey(line.VatRate))
                    taxGroups[line.VatRate] = (0, 0);

                var current = taxGroups[line.VatRate];
                var lineTax = lineTotal * (line.VatRate / 100);
                taxGroups[line.VatRate] = (current.taxableAmount + lineTotal, current.taxAmount + lineTax);
            }
        }

        var taxTotal = taxGroups.Values.Sum(g => g.taxAmount);

        return (lineExtensionTotal, taxTotal, taxGroups);
    }

    #endregion

    #region Helpers

    private static string FormatCUI(string? cui)
    {
        var clean = cui?.Trim().ToUpper().Replace(" ", "") ?? "";
        if (!clean.StartsWith("RO"))
            clean = "RO" + clean;
        return clean;
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatQuantity(decimal value)
    {
        return value.ToString("0.0000", CultureInfo.InvariantCulture);
    }

    private static string GetTaxCategoryCode(decimal vatRate)
    {
        return vatRate == 0 ? "Z" : "S";
    }

    private static string MapUnitCode(string? unit)
    {
        return unit?.ToLower() switch
        {
            "buc" or "bucata" or "bucati" => "EA",
            "kg" or "kilogram" => "KGM",
            "l" or "litru" or "litri" => "LTR",
            "m" or "metru" or "metri" => "MTR",
            "mp" or "m2" => "MTK",
            "ora" or "ore" => "HUR",
            "zi" or "zile" => "DAY",
            "luna" or "luni" => "MON",
            _ => "EA"
        };
    }

    #endregion
}


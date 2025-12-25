using System.Text;
using System.Xml;
using System.Xml.Serialization;
using EasyBilling.ANAFIntegration.EFactura.Models;
using EasyBilling.Domain.Models;

namespace EasyBilling.ANAFIntegration.EFactura
{
    public class EFacturaXmlGenerator : IEFacturaXmlGenerator
    {
        public string GenerateXml(Invoice invoice)
        {
            if (invoice.Company == null)
                throw new ArgumentException("Invoice must have Company information loaded");

            if (invoice.Client == null)
                throw new ArgumentException("Invoice must have Client information loaded");

            var ublInvoice = ConvertToUBL(invoice);

            return SerializeToXml(ublInvoice);
        }

        public void GenerateXmlFile(Invoice invoice, string filePath)
        {
            var xml = GenerateXml(invoice);
            File.WriteAllText(filePath, xml, Encoding.UTF8);
        }

        private UBLInvoice ConvertToUBL(Invoice invoice)
        {
            var ublInvoice = new UBLInvoice
            {
                ID = $"{invoice.Series}{invoice.Number}",
                IssueDate = invoice.Date.ToString("yyyy-MM-dd"),
                DocumentCurrencyCode = "RON",

                AccountingSupplierParty = CreateSupplierParty(invoice.Company),
                AccountingCustomerParty = CreateCustomerParty(invoice.Client)
            };

            // Add invoice lines
            if (invoice.InvoiceLines != null && invoice.InvoiceLines.Any())
            {
                var lineNumber = 1;
                foreach (var line in invoice.InvoiceLines)
                {
                    ublInvoice.InvoiceLine.Add(CreateInvoiceLine(line, lineNumber));
                    lineNumber++;
                }
            }

            // Calculate totals
            CalculateTotals(invoice, ublInvoice);

            return ublInvoice;
        }

        private UBLSupplierParty CreateSupplierParty(Company company)
        {
            return new UBLSupplierParty
            {
                Party = new UBLParty
                {
                    EndpointID = new UBLEndpointID
                    {
                        SchemeID = "9958",
                        Value = FormatCUI(company.CUI)
                    },
                    PartyIdentification = new List<UBLPartyIdentification>
                    {
                        new UBLPartyIdentification
                        {
                            ID = new UBLIdentifier
                            {
                                SchemeID = "0088",
                                Value = FormatCUI(company.CUI)
                            }
                        }
                    },
                    PartyName = new UBLPartyName
                    {
                        Name = company.Name
                    },
                    PostalAddress = new UBLPostalAddress
                    {
                        StreetName = company.Address,
                        CountrySubentity = company.County,
                        Country = new UBLCountry { IdentificationCode = "RO" }
                    },
                    PartyTaxScheme = new List<UBLPartyTaxScheme>
                    {
                        new UBLPartyTaxScheme
                        {
                            CompanyID = FormatCUI(company.CUI),
                            TaxScheme = new UBLTaxScheme { ID = "VAT" }
                        }
                    },
                    PartyLegalEntity = new UBLPartyLegalEntity
                    {
                        RegistrationName = company.Name,
                        CompanyID = string.IsNullOrEmpty(company.RegNumber)
                            ? null
                            : new UBLIdentifier { Value = company.RegNumber }
                    }
                }
            };
        }

        private UBLCustomerParty CreateCustomerParty(Client client)
        {
            return new UBLCustomerParty
            {
                Party = new UBLParty
                {
                    EndpointID = new UBLEndpointID
                    {
                        SchemeID = "9958",
                        Value = FormatCUI(client.CUI)
                    },
                    PartyIdentification = new List<UBLPartyIdentification>
                    {
                        new UBLPartyIdentification
                        {
                            ID = new UBLIdentifier
                            {
                                SchemeID = "0088",
                                Value = FormatCUI(client.CUI)
                            }
                        }
                    },
                    PartyName = new UBLPartyName
                    {
                        Name = client.Name
                    },
                    PostalAddress = new UBLPostalAddress
                    {
                        StreetName = client.Address,
                        CountrySubentity = client.County,
                        Country = new UBLCountry { IdentificationCode = "RO" }
                    },
                    PartyTaxScheme = new List<UBLPartyTaxScheme>
                    {
                        new UBLPartyTaxScheme
                        {
                            CompanyID = FormatCUI(client.CUI),
                            TaxScheme = new UBLTaxScheme { ID = "VAT" }
                        }
                    },
                    PartyLegalEntity = new UBLPartyLegalEntity
                    {
                        RegistrationName = client.Name,
                        CompanyID = string.IsNullOrEmpty(client.RegNumber)
                            ? null
                            : new UBLIdentifier { Value = client.RegNumber }
                    }
                }
            };
        }

        private UBLInvoiceLine CreateInvoiceLine(InvoiceLine line, int lineNumber)
        {
            var lineExtensionAmount = line.Quantity * line.UnitPrice;

            return new UBLInvoiceLine
            {
                ID = lineNumber.ToString(),
                InvoicedQuantity = new UBLQuantity
                {
                    UnitCode = MapUnitCode(line.Unit),
                    Value = line.Quantity
                },
                LineExtensionAmount = new UBLAmount
                {
                    CurrencyID = "RON",
                    Value = lineExtensionAmount
                },
                Item = new UBLItem
                {
                    Name = line.Description,
                    ClassifiedTaxCategory = new UBLClassifiedTaxCategory
                    {
                        ID = GetTaxCategoryCode(line.VatRate),
                        Percent = line.VatRate,
                        TaxScheme = new UBLTaxScheme { ID = "VAT" }
                    }
                },
                Price = new UBLPrice
                {
                    PriceAmount = new UBLAmount
                    {
                        CurrencyID = "RON",
                        Value = line.UnitPrice
                    }
                }
            };
        }

        private void CalculateTotals(Invoice invoice, UBLInvoice ublInvoice)
        {
            decimal totalLineExtension = 0;
            var taxGroups = new Dictionary<decimal, decimal>(); // VatRate -> TaxableAmount

            if (invoice.InvoiceLines != null)
            {
                foreach (var line in invoice.InvoiceLines)
                {
                    var lineTotal = line.Quantity * line.UnitPrice;
                    totalLineExtension += lineTotal;

                    if (!taxGroups.ContainsKey(line.VatRate))
                        taxGroups[line.VatRate] = 0;

                    taxGroups[line.VatRate] += lineTotal;
                }
            }

            decimal totalTaxAmount = 0;
            var taxSubtotals = new List<UBLTaxSubtotal>();

            foreach (var taxGroup in taxGroups)
            {
                var vatRate = taxGroup.Key;
                var taxableAmount = taxGroup.Value;
                var taxAmount = taxableAmount * (vatRate / 100);
                totalTaxAmount += taxAmount;

                taxSubtotals.Add(new UBLTaxSubtotal
                {
                    TaxableAmount = new UBLAmount { CurrencyID = "RON", Value = taxableAmount },
                    TaxAmount = new UBLAmount { CurrencyID = "RON", Value = taxAmount },
                    TaxCategory = new UBLTaxCategory
                    {
                        ID = GetTaxCategoryCode(vatRate),
                        Percent = vatRate,
                        TaxScheme = new UBLTaxScheme { ID = "VAT" }
                    }
                });
            }

            ublInvoice.TaxTotal = new List<UBLTaxTotal>
            {
                new UBLTaxTotal
                {
                    TaxAmount = new UBLAmount { CurrencyID = "RON", Value = totalTaxAmount },
                    TaxSubtotal = taxSubtotals
                }
            };

            ublInvoice.LegalMonetaryTotal = new UBLMonetaryTotal
            {
                LineExtensionAmount = new UBLAmount { CurrencyID = "RON", Value = totalLineExtension },
                TaxExclusiveAmount = new UBLAmount { CurrencyID = "RON", Value = totalLineExtension },
                TaxInclusiveAmount = new UBLAmount { CurrencyID = "RON", Value = totalLineExtension + totalTaxAmount },
                PayableAmount = new UBLAmount { CurrencyID = "RON", Value = totalLineExtension + totalTaxAmount }
            };
        }

        private string FormatCUI(string cui)
        {
            // Ensure CUI starts with RO for Romanian tax numbers
            var cleanCui = cui?.Trim().ToUpper() ?? "";
            if (!cleanCui.StartsWith("RO"))
                cleanCui = "RO" + cleanCui;
            return cleanCui;
        }

        private string MapUnitCode(string unit)
        {
            // Map common Romanian units to UN/ECE Recommendation 20 codes
            return unit?.ToLower() switch
            {
                "buc" or "bucata" or "bucati" => "H87", // Piece
                "kg" or "kilogram" => "KGM",
                "l" or "litru" or "litri" => "LTR",
                "m" or "metru" or "metri" => "MTR",
                "mp" or "m2" => "MTK", // Square meter
                "ora" or "ore" => "HUR", // Hour
                "zi" or "zile" => "DAY",
                "luna" or "luni" => "MON",
                _ => "H87" // Default to piece
            };
        }

        private string GetTaxCategoryCode(decimal vatRate)
        {
            // S = Standard rate, Z = Zero rated, E = Exempt
            if (vatRate == 0)
                return "Z";
            return "S";
        }

        private string SerializeToXml(UBLInvoice invoice)
        {
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add("", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
            namespaces.Add("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            namespaces.Add("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");

            var serializer = new XmlSerializer(typeof(UBLInvoice));
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, settings);

            serializer.Serialize(xmlWriter, invoice, namespaces);
            return stringWriter.ToString();
        }
    }
}

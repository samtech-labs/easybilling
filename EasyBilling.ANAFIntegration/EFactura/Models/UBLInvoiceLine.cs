using System.Globalization;
using System.Xml.Serialization;

namespace EasyBilling.ANAFIntegration.EFactura.Models
{
    public class UBLInvoiceLine
    {
        [XmlElement("ID", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string ID { get; set; } = string.Empty;

        [XmlElement("InvoicedQuantity", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLQuantity InvoicedQuantity { get; set; } = new();

        [XmlElement("LineExtensionAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount LineExtensionAmount { get; set; } = new();

        [XmlElement("Item", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLItem Item { get; set; } = new();

        [XmlElement("Price", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLPrice Price { get; set; } = new();
    }

    public class UBLQuantity
    {
        [XmlAttribute("unitCode")]
        public string UnitCode { get; set; } = string.Empty;

        [XmlText]
        public string ValueString
        {
            get => Value.ToString("0.##", CultureInfo.InvariantCulture);
            set => Value = decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        [XmlIgnore]
        public decimal Value { get; set; }
    }

    public class UBLAmount
    {
        [XmlAttribute("currencyID")]
        public string CurrencyID { get; set; } = "RON";

        [XmlText]
        public string ValueString
        {
            get => Value.ToString("0.00", CultureInfo.InvariantCulture);
            set => Value = decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        [XmlIgnore]
        public decimal Value { get; set; }
    }

    public class UBLItem
    {
        [XmlElement("Name", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string Name { get; set; } = string.Empty;

        [XmlElement("ClassifiedTaxCategory", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLClassifiedTaxCategory ClassifiedTaxCategory { get; set; } = new();
    }

    public class UBLClassifiedTaxCategory
    {
        [XmlElement("ID", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string ID { get; set; } = "S";

        [XmlElement("Percent", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string PercentString
        {
            get => Percent.ToString("0.##", CultureInfo.InvariantCulture);
            set => Percent = decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        [XmlIgnore]
        public decimal Percent { get; set; }

        [XmlElement("TaxScheme", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLTaxScheme TaxScheme { get; set; } = new();
    }

    public class UBLPrice
    {
        [XmlElement("PriceAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount PriceAmount { get; set; } = new();
    }
}

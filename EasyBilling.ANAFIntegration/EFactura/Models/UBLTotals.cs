using System.Globalization;
using System.Xml.Serialization;

namespace EasyBilling.ANAFIntegration.EFactura.Models
{
    public class UBLTaxTotal
    {
        [XmlElement("TaxAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount TaxAmount { get; set; } = new();

        [XmlElement("TaxSubtotal", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public List<UBLTaxSubtotal> TaxSubtotal { get; set; } = new();
    }

    public class UBLTaxSubtotal
    {
        [XmlElement("TaxableAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount TaxableAmount { get; set; } = new();

        [XmlElement("TaxAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount TaxAmount { get; set; } = new();

        [XmlElement("TaxCategory", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLTaxCategory TaxCategory { get; set; } = new();
    }

    public class UBLTaxCategory
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

    public class UBLMonetaryTotal
    {
        [XmlElement("LineExtensionAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount LineExtensionAmount { get; set; } = new();

        [XmlElement("TaxExclusiveAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount TaxExclusiveAmount { get; set; } = new();

        [XmlElement("TaxInclusiveAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount TaxInclusiveAmount { get; set; } = new();

        [XmlElement("PayableAmount", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLAmount PayableAmount { get; set; } = new();
    }
}

using System.Xml.Serialization;

namespace EasyBilling.ANAFIntegration.EFactura.Models
{
    public class UBLSupplierParty
    {
        [XmlElement("Party", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLParty Party { get; set; } = new();
    }

    public class UBLCustomerParty
    {
        [XmlElement("Party", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLParty Party { get; set; } = new();
    }

    public class UBLParty
    {
        [XmlElement("EndpointID", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLEndpointID? EndpointID { get; set; }

        [XmlElement("PartyIdentification", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public List<UBLPartyIdentification> PartyIdentification { get; set; } = new();

        [XmlElement("PartyName", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLPartyName? PartyName { get; set; }

        [XmlElement("PostalAddress", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLPostalAddress? PostalAddress { get; set; }

        [XmlElement("PartyTaxScheme", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public List<UBLPartyTaxScheme> PartyTaxScheme { get; set; } = new();

        [XmlElement("PartyLegalEntity", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLPartyLegalEntity? PartyLegalEntity { get; set; }
    }

    public class UBLEndpointID
    {
        [XmlAttribute("schemeID")]
        public string SchemeID { get; set; } = "9958"; // RO VAT number scheme

        [XmlText]
        public string Value { get; set; } = string.Empty;
    }

    public class UBLPartyIdentification
    {
        [XmlElement("ID", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLIdentifier ID { get; set; } = new();
    }

    public class UBLIdentifier
    {
        [XmlAttribute("schemeID")]
        public string? SchemeID { get; set; }

        [XmlText]
        public string Value { get; set; } = string.Empty;
    }

    public class UBLPartyName
    {
        [XmlElement("Name", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string Name { get; set; } = string.Empty;
    }

    public class UBLPostalAddress
    {
        [XmlElement("StreetName", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string? StreetName { get; set; }

        [XmlElement("CityName", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string? CityName { get; set; }

        [XmlElement("CountrySubentity", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string? CountrySubentity { get; set; }

        [XmlElement("Country", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLCountry Country { get; set; } = new();
    }

    public class UBLCountry
    {
        [XmlElement("IdentificationCode", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string IdentificationCode { get; set; } = "RO";
    }

    public class UBLPartyTaxScheme
    {
        [XmlElement("CompanyID", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string CompanyID { get; set; } = string.Empty;

        [XmlElement("TaxScheme", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2")]
        public UBLTaxScheme TaxScheme { get; set; } = new();
    }

    public class UBLTaxScheme
    {
        [XmlElement("ID", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string ID { get; set; } = "VAT";
    }

    public class UBLPartyLegalEntity
    {
        [XmlElement("RegistrationName", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public string RegistrationName { get; set; } = string.Empty;

        [XmlElement("CompanyID", Namespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2")]
        public UBLIdentifier? CompanyID { get; set; }
    }
}

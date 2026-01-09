namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models
{
    public class CompanyDetails
    {
        // General Info
        public string? CUI { get; set; }
        public DateTime? QueryDate { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? Phone { get; set; }
        public string? Fax { get; set; }
        public string? PostalCode { get; set; }
        public string? LegalDocument { get; set; }
        public string? RegistrationStatus { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string? CAENCode { get; set; }
        public string? IBAN { get; set; }
        public bool IsEFacturaActive { get; set; }
        public string? TaxAuthority { get; set; }
        public string? OwnershipForm { get; set; }
        public string? OrganizationForm { get; set; }
        public string? LegalForm { get; set; }

        // VAT Registration (inregistrare_scop_Tva)
        public bool IsVatPayer { get; set; }
        public DateTime? VatStartDate { get; set; }
        public DateTime? VatEndDate { get; set; }
        public DateTime? VatTaxYearDate { get; set; }
        public string? VatMessage { get; set; }

        // Cash-basis VAT (inregistrare_RTVAI)
        public DateTime? CashVatStartDate { get; set; }
        public DateTime? CashVatEndDate { get; set; }
        public DateTime? CashVatUpdateDate { get; set; }
        public DateTime? CashVatPublishDate { get; set; }
        public string? CashVatDocumentType { get; set; }
        public bool IsCashVatPayer { get; set; }

        // Inactive Status (stare_inactiv)
        public DateTime? InactivationDate { get; set; }
        public DateTime? ReactivationDate { get; set; }
        public DateTime? InactivePublishDate { get; set; }
        public DateTime? DeregistrationDate { get; set; }
        public bool IsInactive { get; set; }

        // Split VAT (inregistrare_SplitTVA)
        public DateTime? SplitVatStartDate { get; set; }
        public DateTime? SplitVatCancelDate { get; set; }
        public bool IsSplitVatPayer { get; set; }

        // Registered Address (adresa_sediu_social)
        public AddressDetails? RegisteredAddress { get; set; }

        // Tax Domicile Address (adresa_domiciliu_fiscal)
        public AddressDetails? TaxDomicileAddress { get; set; }
    }

    public class AddressDetails
    {
        public string? Street { get; set; }
        public string? StreetNumber { get; set; }
        public string? City { get; set; }
        public string? CityCode { get; set; }
        public string? County { get; set; }
        public string? CountyCode { get; set; }
        public string? CountyAutoCode { get; set; }
        public string? Country { get; set; }
        public string? Details { get; set; }
        public string? PostalCode { get; set; }

        public string? FormattedAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Street)) parts.Add(Street);
                if (!string.IsNullOrEmpty(StreetNumber)) parts.Add($"Nr. {StreetNumber}");
                if (!string.IsNullOrEmpty(Details)) parts.Add(Details);
                if (!string.IsNullOrEmpty(City)) parts.Add(City);
                if (!string.IsNullOrEmpty(County)) parts.Add($"Jud. {County}");
                if (!string.IsNullOrEmpty(PostalCode)) parts.Add(PostalCode);
                return parts.Count > 0 ? string.Join(", ", parts) : null;
            }
        }
    }
}

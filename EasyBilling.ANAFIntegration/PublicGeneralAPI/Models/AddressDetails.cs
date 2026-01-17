namespace EasyBilling.ANAFIntegration.PublicGeneralAPI.Models;

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

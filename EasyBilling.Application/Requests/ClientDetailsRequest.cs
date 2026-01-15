namespace EasyBilling.Application.Requests;

public class ClientDetailsRequest
{
    public string? Name { get; set; }

    public string? Cui { get; set; }

    public string? Address { get; set; }

    public string? County { get; set; }

    public string? RegNumber { get; set; }

    public string? Iban { get; set; }

    public string? Bank { get; set; }
}

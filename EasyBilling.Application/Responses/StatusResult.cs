namespace EasyBilling.Application.Responses;

public sealed class StatusResult
{
    public string Stare { get; set; } = String.Empty;

    public string? IdDescarcare { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsTechnicalError { get; set; }
}
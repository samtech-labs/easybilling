namespace EasyBilling.ANAFIntegration.EFactura.Models;

public class EFacturaDownloadResponse
{
    public bool Success { get; set; }
    public byte[]? ZipContent { get; set; }
    public string? ErrorMessage { get; set; }
}
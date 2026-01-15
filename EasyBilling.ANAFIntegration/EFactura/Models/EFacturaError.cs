using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.EFactura.Models;

public class EFacturaError
{
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }
}


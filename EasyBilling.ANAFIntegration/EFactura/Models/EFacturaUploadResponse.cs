using System.Text.Json.Serialization;

namespace EasyBilling.ANAFIntegration.EFactura.Models
{
    public class EFacturaUploadResponse
    {
        [JsonPropertyName("dateResponse")]
        public DateTime DateResponse { get; set; }

        [JsonPropertyName("ExecutionStatus")]
        public int ExecutionStatus { get; set; }

        [JsonPropertyName("index_incarcare")]
        public string UploadIndex { get; set; } = string.Empty;

        [JsonPropertyName("Messages")]
        public List<string>? Messages { get; set; }

        [JsonPropertyName("Errors")]
        public List<EFacturaError>? Errors { get; set; }

        public bool IsSuccess => ExecutionStatus == 0 && (Errors == null || Errors.Count == 0);
    }

    public class EFacturaError
    {
        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }
    }
}

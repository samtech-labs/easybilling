using System.Text.Json.Serialization;

namespace EasyBilling.Application.Requests
{
    public class CreditNoteLinesRequest
    {
        public string Description { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }

        [JsonPropertyName("vat")]
        public decimal Vat { get; set; }

        public string Unit { get; set; } = "buc";
    }
}

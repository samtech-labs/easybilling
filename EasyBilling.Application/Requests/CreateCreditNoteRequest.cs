namespace EasyBilling.Application.Requests;

public class CreateCreditNoteRequest
{
    public Guid OriginalInvoiceId { get; set; }

    public string? Series { get; set; }

    public string? Number { get; set; }

    public List<CreditNoteLinesRequest>? Lines { get; set; }
}

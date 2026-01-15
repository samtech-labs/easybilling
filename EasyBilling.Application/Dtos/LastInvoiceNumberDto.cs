namespace EasyBilling.Application.Dtos;

public class LastInvoiceNumberDto
{
    public string Series { get; set; } = default!;

    public int Number { get; set; }
}

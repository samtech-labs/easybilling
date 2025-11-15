

namespace EasyBilling.Application.Dtos
{
    public class ClientDto
    {
        public string Name { get; set; } = default!;
        public string CUI { get; set; } = default!;
        public string RegNumber { get; set; } = default!;
        public string Address { get; set; } = default!;
        public string IBAN { get; set; } = default!;
        public string Bank { get; set; } = default!;
    }
}

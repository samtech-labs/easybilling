namespace EasyBilling.Domain.Models
{
    public class Company
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string VatCode { get; set; }
    }
}

namespace EasyBilling.Domain.Models
{
    public class Company
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string RegNumber { get; set; }
        public string CUI { get; set; }
        public string IBAN { get; set; }
        public string Bank { get; set; }
    }
}

namespace EasyBilling.Domain.Models
{
    public class MembershipType
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required decimal Price { get; set; }
        public required int MaxInvoicesPerMonth { get; set; }
        public required bool EFacturaActive { get; set; }
        public required int DurationInDays { get; set; }
    }
}

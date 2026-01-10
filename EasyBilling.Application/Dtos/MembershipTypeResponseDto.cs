namespace EasyBilling.Application.Dtos
{
    public class MembershipTypeResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
        public int MaxInvoicesPerMonth { get; set; }
        public bool EFacturaActive { get; set; }
        public int DurationInDays { get; set; }
    }
}

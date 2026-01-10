namespace EasyBilling.Application.Dtos
{
    public class MembershipResponseDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = default!;
        public Guid MembershipTypeId { get; set; }
        public string MembershipTypeName { get; set; } = default!;
        public decimal Price { get; set; }
        public int MaxInvoicesPerMonth { get; set; }
        public bool EFacturaActive { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
    }
}

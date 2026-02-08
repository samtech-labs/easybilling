namespace EasyBilling.Domain.Models
{
    public class Membership
    {
        public Guid Id { get; set; }
        public required Guid UserId { get; set; }
        public required Guid MembershipTypeId { get; set; }
        public required DateTime StartDate { get; set; }
        public required DateTime EndDate { get; set; }

        public User User { get; set; } = null!;
        public MembershipType MembershipType { get; set; } = null!;
    }
}

using System.ComponentModel.DataAnnotations;

namespace EasyBilling.Application.Requests
{
    public class AssignMembershipRequest
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid MembershipTypeId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }
    }
}

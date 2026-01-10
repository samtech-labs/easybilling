using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "ADMIN")]
    public class MembershipController : ControllerBase
    {
        private readonly IMembershipService _membershipService;

        public MembershipController(IMembershipService membershipService)
        {
            _membershipService = membershipService;
        }

        [HttpGet]
        [Route("GetAll")]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            try
            {
                var memberships = await _membershipService.GetAllAsync(cancellationToken);
                return Ok(memberships);
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving memberships.");
            }
        }

        [HttpGet]
        [Route("GetById")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var membership = await _membershipService.GetByIdAsync(id, cancellationToken);
                return Ok(membership);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving the membership.");
            }
        }

        [HttpGet]
        [Route("GetByUserId")]
        public async Task<IActionResult> GetByUserId(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                var memberships = await _membershipService.GetByUserIdAsync(userId, cancellationToken);
                return Ok(memberships);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving user memberships.");
            }
        }

        [HttpGet]
        [Route("GetActiveMembershipByUserId")]
        public async Task<IActionResult> GetActiveMembershipByUserId(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                var membership = await _membershipService.GetActiveMembershipByUserIdAsync(userId, cancellationToken);
                if (membership == null)
                {
                    return NotFound(new { message = "No active membership found for this user." });
                }
                return Ok(membership);
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving the active membership.");
            }
        }

        [HttpPost]
        [Route("Assign")]
        public async Task<IActionResult> Assign([FromBody] AssignMembershipRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var membership = await _membershipService.AssignMembershipAsync(request, cancellationToken);
                return Ok(membership);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while assigning the membership.");
            }
        }

        [HttpDelete]
        [Route("Delete")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _membershipService.DeleteAsync(id, cancellationToken);
                return Ok(new { message = "Membership deleted successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while deleting the membership.");
            }
        }
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EasyBilling.Application.Interfaces;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CompanyController(ICompanyService companyService) : ControllerBase
    {
        private readonly ICompanyService _companyService = companyService;

        [HttpGet]
        [Route("GetAllCompanies")]
        public async Task<IActionResult> GetCompanies()
        {
            // TODO: Move user identification to a middleware or service
            // to avoid repeating this logic in every controller method.
            // Also check the current implementation for CurrentUserService.
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized();
            }

            var companies = await _companyService.GetCompaniesByUserAsync(userId);
            return Ok(companies);
        }

        [HttpPost]
        [Route("CreateCompany")]
        public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest createCompanyRequest)
        {
            try
            {
                var company = await _companyService.CreateCompanyAsync(createCompanyRequest);
                return Ok(company);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while creating the company.");
            }
        }

        [HttpGet]
        [Route("GetCompanyDetailsFromAnaf")]
        public async Task<IActionResult> GetCompanyDetailsFromAnaf(string cui)
        {
            try
            {
                var companyDetails = await _companyService.GetCompanyDetailsFromAnaf(cui);
                return Ok(companyDetails);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving company details from ANAF.");
            }
        }

        [HttpDelete]
        [Route("DeleteCompany")]
        public async Task<IActionResult> DeleteCompany(Guid companyId)
        {
            try
            {
                await _companyService.DeleteCompanyAsync(companyId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while deleting the company.");
            }
        }
    }
}

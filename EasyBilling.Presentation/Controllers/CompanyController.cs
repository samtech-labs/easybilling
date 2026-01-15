using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CompanyController(ICompanyService companyService) : ControllerBase
{
    private readonly ICompanyService _companyService = companyService;

    [HttpGet]
    [Route("GetAllCompanies")]
    public async Task<IActionResult> GetCompanies(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string sortOrder = "desc",
        [FromQuery] bool? isVatPayer = null,
        [FromQuery] bool? isEFacturaActive = null,
        [FromQuery] string? county = null)
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

        var hasPaginationParams = !string.IsNullOrEmpty(searchTerm) || !string.IsNullOrEmpty(sortBy) || sortOrder != "desc" ||
            isVatPayer.HasValue || isEFacturaActive.HasValue || !string.IsNullOrEmpty(county);

        if (hasPaginationParams)
        {
            try
            {
                var filter = new CompanyPaginationFilter
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    IsVatPayer = isVatPayer,
                    IsEFacturaActive = isEFacturaActive,
                    County = county
                };

                if (!filter.IsValid)
                {
                    return BadRequest(new { message = "Invalid pagination parameters. PageNumber must be >= 1 and PageSize must be between 1 and 100." });
                }

                var result = await _companyService.GetCompaniesByUserPaginatedAsync(userId, filter);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving companies.");
            }
        }

        try
        {
            var companies = await _companyService.GetCompaniesByUserAsync(userId);
            return Ok(companies);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while retrieving companies.");
        }
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

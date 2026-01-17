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
public class CompanyController(ICompanyService companyService, ILogger<CompanyController> logger) : ControllerBase
{
    private readonly ICompanyService _companyService = companyService;
    private readonly ILogger<CompanyController> _logger = logger;

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
        _logger.LogInformation("GetCompanies request - PageNumber: {PageNumber}, PageSize: {PageSize}",
            pageNumber, pageSize);

        try
        {
            // TODO: Move user identification to a middleware or service
            // to avoid repeating this logic in every controller method.
            // Also check the current implementation for CurrentUserService.
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)
                ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("GetCompanies request with invalid user identification");
                return Unauthorized();
            }

            _logger.LogDebug("User identified for GetCompanies request: {UserId}", userId);

            var hasPaginationParams = !string.IsNullOrEmpty(searchTerm) || !string.IsNullOrEmpty(sortBy) || sortOrder != "desc" ||
                isVatPayer.HasValue || isEFacturaActive.HasValue || !string.IsNullOrEmpty(county);

            if (hasPaginationParams)
            {
                _logger.LogDebug("Applying pagination filters - SearchTerm: {SearchTerm}, SortBy: {SortBy}, IsVatPayer: {IsVatPayer}, IsEFacturaActive: {IsEFacturaActive}, County: {County}",
                    searchTerm ?? "none", sortBy ?? "none", isVatPayer.HasValue ? isVatPayer.Value.ToString() : "none",
                    isEFacturaActive.HasValue ? isEFacturaActive.Value.ToString() : "none", county ?? "none");

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
                        _logger.LogWarning("Invalid pagination parameters - PageNumber: {PageNumber}, PageSize: {PageSize}",
                            pageNumber, pageSize);
                        return BadRequest(new { message = "Invalid pagination parameters. PageNumber must be >= 1 and PageSize must be between 1 and 100." });
                    }

                    var result = await _companyService.GetCompaniesByUserPaginatedAsync(userId, filter);

                    _logger.LogInformation("Retrieved {ItemCount} companies out of {TotalCount} for user {UserId} (page {PageNumber})",
                        result.Items.Count, result.TotalCount, userId, pageNumber);

                    return Ok(result);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Business logic error retrieving paginated companies for user {UserId}",
                        userId);
                    return BadRequest(new { message = ex.Message });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving paginated companies for user {UserId}", userId);
                    return StatusCode(500, new { message = "An error occurred while retrieving companies." });
                }
            }

            _logger.LogDebug("Retrieving all companies for user {UserId}", userId);

            try
            {
                var companies = await _companyService.GetCompaniesByUserAsync(userId);

                _logger.LogInformation("Retrieved {CompanyCount} companies for user {UserId}",
                    companies.Count, userId);

                return Ok(companies);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Business logic error retrieving all companies for user {UserId}",
                    userId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all companies for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while retrieving companies." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetCompanies");
            return StatusCode(500, new { message = "An error occurred while retrieving companies." });
        }
    }

    [HttpPost]
    [Route("CreateCompany")]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest createCompanyRequest)
    {
        _logger.LogInformation("CreateCompany request - CompanyName: {CompanyName}, CUI: {CUI}",
            createCompanyRequest?.Name ?? "unknown", createCompanyRequest?.CUI ?? "unknown");

        try
        {
            if (createCompanyRequest == null)
            {
                _logger.LogWarning("CreateCompany request with null request body");
                return BadRequest(new { message = "Request body is required" });
            }

            if (string.IsNullOrWhiteSpace(createCompanyRequest.CUI))
            {
                _logger.LogWarning("CreateCompany request with empty CUI");
                return BadRequest(new { message = "Company CUI is required" });
            }

            if (string.IsNullOrWhiteSpace(createCompanyRequest.Name))
            {
                _logger.LogWarning("CreateCompany request with empty Name");
                return BadRequest(new { message = "Company Name is required" });
            }

            _logger.LogDebug("Creating company with CUI: {CUI}", createCompanyRequest.CUI);

            var company = await _companyService.CreateCompanyAsync(createCompanyRequest);

            _logger.LogInformation("Company successfully created - CompanyId: {CompanyId}, Name: {CompanyName}, CUI: {CUI}",
                company.Id, company.Name, company.CUI);

            return Ok(company);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error creating company with CUI: {CUI}",
                createCompanyRequest?.CUI);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company with CUI: {CUI}",
                createCompanyRequest?.CUI);
            return StatusCode(500, new { message = "An error occurred while creating the company." });
        }
    }

    [HttpGet]
    [Route("GetCompanyDetailsFromAnaf")]
    public async Task<IActionResult> GetCompanyDetailsFromAnaf(string cui)
    {
        _logger.LogInformation("GetCompanyDetailsFromAnaf request for CUI: {CUI}", cui);

        try
        {
            if (string.IsNullOrWhiteSpace(cui))
            {
                _logger.LogWarning("GetCompanyDetailsFromAnaf request with empty CUI");
                return BadRequest(new { message = "CUI is required" });
            }

            _logger.LogDebug("Fetching company details from ANAF for CUI: {CUI}", cui);

            var companyDetails = await _companyService.GetCompanyDetailsFromAnaf(cui);

            _logger.LogInformation("Company details successfully retrieved from ANAF for CUI: {CUI}, CompanyName: {CompanyName}",
                cui, companyDetails?.Name ?? "unknown");

            return Ok(companyDetails);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error fetching company details from ANAF for CUI: {CUI}",
                cui);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching company details from ANAF for CUI: {CUI}",
                cui);
            return StatusCode(500, new { message = "An error occurred while retrieving company details from ANAF." });
        }
    }

    [HttpDelete]
    [Route("DeleteCompany")]
    public async Task<IActionResult> DeleteCompany(Guid companyId)
    {
        _logger.LogInformation("DeleteCompany request - CompanyId: {CompanyId}", companyId);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("DeleteCompany request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            _logger.LogDebug("Deleting company {CompanyId}", companyId);

            await _companyService.DeleteCompanyAsync(companyId);

            _logger.LogInformation("Company successfully deleted - CompanyId: {CompanyId}", companyId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error deleting company {CompanyId}",
                companyId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company {CompanyId}", companyId);
            return StatusCode(500, new { message = "An error occurred while deleting the company." });
        }
    }
}

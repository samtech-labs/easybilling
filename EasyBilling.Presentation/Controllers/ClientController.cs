using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ClientController(IClientService clientService, ILogger<ClientController> logger) : ControllerBase
{
    private readonly IClientService _clientService = clientService;
    private readonly ILogger<ClientController> _logger = logger;

    [HttpGet]
    [Route("GetAllClients")]
    public async Task<IActionResult> GetClients(
        [FromQuery] Guid companyId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string sortOrder = "desc",
        [FromQuery] string? city = null)
    {
        _logger.LogInformation("GetClients request for company {CompanyId} - PageNumber: {PageNumber}, PageSize: {PageSize}",
            companyId, pageNumber, pageSize);

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("GetClients request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            var hasPaginationParams = !string.IsNullOrEmpty(searchTerm) || !string.IsNullOrEmpty(sortBy) || sortOrder != "desc" ||
                !string.IsNullOrEmpty(city);

            if (hasPaginationParams)
            {
                _logger.LogDebug("Applying pagination filters - SearchTerm: {SearchTerm}, SortBy: {SortBy}, City: {City}",
                    searchTerm ?? "none", sortBy ?? "none", city ?? "none");

                var filter = new ClientPaginationFilter
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    City = city
                };

                if (!filter.IsValid)
                {
                    _logger.LogWarning("Invalid pagination parameters - PageNumber: {PageNumber}, PageSize: {PageSize}",
                        pageNumber, pageSize);
                    return BadRequest(new { message = "Invalid pagination parameters. PageNumber must be >= 1 and PageSize must be between 1 and 100." });
                }

                var result = await _clientService.GetClientsByCompanyIdPaginatedAsync(companyId, filter);

                _logger.LogInformation("Retrieved {ItemCount} clients out of {TotalCount} for company {CompanyId} (page {PageNumber})",
                    result.Items.Count, result.TotalCount, companyId, pageNumber);

                return Ok(result);
            }

            _logger.LogDebug("Retrieving all clients for company {CompanyId}", companyId);

            var clients = await _clientService.GetAllClientsByCompanyIdAsync(companyId);

            _logger.LogInformation("Retrieved {ClientCount} clients for company {CompanyId}",
                clients.Count, companyId);

            return Ok(clients);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error retrieving clients for company {CompanyId}",
                companyId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clients for company {CompanyId}", companyId);
            return StatusCode(500, new { message = "An error occurred while retrieving clients." });
        }
    }

    [HttpPost]
    [Route("CreateClient")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest createClientRequest, Guid companyId)
    {
        _logger.LogInformation("CreateClient request for company {CompanyId}, client CUI: {ClientCUI}",
            companyId, createClientRequest?.CUI ?? "unknown");

        try
        {
            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("CreateClient request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            if (createClientRequest == null)
            {
                _logger.LogWarning("CreateClient request with null request body");
                return BadRequest(new { message = "Request body is required" });
            }

            if (string.IsNullOrWhiteSpace(createClientRequest.CUI))
            {
                _logger.LogWarning("CreateClient request with empty CUI for company {CompanyId}", companyId);
                return BadRequest(new { message = "Client CUI is required" });
            }

            if (string.IsNullOrWhiteSpace(createClientRequest.Name))
            {
                _logger.LogWarning("CreateClient request with empty Name for company {CompanyId}", companyId);
                return BadRequest(new { message = "Client Name is required" });
            }

            _logger.LogDebug("Creating client with CUI {ClientCUI} for company {CompanyId}",
                createClientRequest.CUI, companyId);

            var client = await _clientService.CreateClientAsync(createClientRequest, companyId);

            _logger.LogInformation("Client successfully created - ClientId: {ClientId}, Name: {ClientName}, CUI: {ClientCUI}",
                client.Id, client.Name, client.CUI);

            return Ok(client);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error creating client for company {CompanyId}",
                companyId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client for company {CompanyId}",
                companyId);
            return StatusCode(500, new { message = "An error occurred while creating the client." });
        }
    }

    [HttpDelete]
    [Route("DeleteClient")]
    public async Task<IActionResult> DeleteClient(Guid clientId, Guid companyId)
    {
        _logger.LogInformation("DeleteClient request - ClientId: {ClientId}, CompanyId: {CompanyId}",
            clientId, companyId);

        try
        {
            if (clientId == Guid.Empty)
            {
                _logger.LogWarning("DeleteClient request with empty ClientId");
                return BadRequest(new { message = "ClientId is required" });
            }

            if (companyId == Guid.Empty)
            {
                _logger.LogWarning("DeleteClient request with empty CompanyId");
                return BadRequest(new { message = "CompanyId is required" });
            }

            _logger.LogDebug("Deleting client {ClientId} from company {CompanyId}",
                clientId, companyId);

            await _clientService.DeleteClientAsync(clientId, companyId);

            _logger.LogInformation("Client successfully deleted - ClientId: {ClientId} from company {CompanyId}",
                clientId, companyId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business logic error deleting client {ClientId} from company {CompanyId}",
                clientId, companyId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting client {ClientId} from company {CompanyId}",
                clientId, companyId);
            return StatusCode(500, new { message = "An error occurred while deleting the client." });
        }
    }
}
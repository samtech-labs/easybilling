using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClientController(IClientService clientService) : ControllerBase
    {
        private readonly IClientService _clientService = clientService;

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
            try
            {
                var hasPaginationParams = !string.IsNullOrEmpty(searchTerm) || !string.IsNullOrEmpty(sortBy) || sortOrder != "desc" ||
                    !string.IsNullOrEmpty(city);

                if (hasPaginationParams)
                {
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
                        return BadRequest(new { message = "Invalid pagination parameters. PageNumber must be >= 1 and PageSize must be between 1 and 100." });
                    }

                    var result = await _clientService.GetClientsByCompanyIdPaginatedAsync(companyId, filter);
                    return Ok(result);
                }

                var clients = await _clientService.GetAllClientsByCompanyIdAsync(companyId);
                return Ok(clients);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while retrieving clients.");
            }
        }

        [HttpPost]
        [Route("CreateClient")]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest createClientRequest, Guid companyId)
        {
            try
            {
                var client = await _clientService.CreateClientAsync(createClientRequest, companyId);
                return Ok(client);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while creating the client.");
            }
        }

        [HttpDelete]
        [Route("DeleteClient")]
        public async Task<IActionResult> DeleteClient(Guid clientId, Guid companyId)
        {
            try
            {
                await _clientService.DeleteClientAsync(clientId, companyId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while deleting the client.");
            }
        }
    }
}

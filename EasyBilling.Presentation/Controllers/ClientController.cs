using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ClientController(IClientService clientService) : Controller
    {
        private readonly IClientService _clientService = clientService;

        [HttpGet]
        [Route("GetAllClients")]
        public async Task<IActionResult> GetClients(Guid companyId)
        {
            try
            {
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
        
        [HttpGet]
        [Route("GetAllClientsPaged")]
        public async Task<IActionResult> GetClientsPaged(Guid companyId, [FromQuery] int page, int pageSize, 
            CancellationToken cancellationToken)
        {
            try
            {
                var clients = await _clientService.GetClientsByCompanyIdPagedAsync(companyId,
                     new PageRequest{Page = page, PageSize = pageSize}, cancellationToken);
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

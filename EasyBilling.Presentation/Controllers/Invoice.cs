using EasyBilling.Application.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Invoice : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreateInvoice(Guid companyId, [FromBody] CreateInvoiceRequest request)
        {
            // Implementation for creating an invoice goes here.
            return Ok("Invoice created successfully.");
        }

        [HttpGet]
        public IActionResult GetInvoice()
        {
            return Ok("Hello world");
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers.V1
{
    [Route("api/[controller]")]
    [ApiController]
    public class Invoice : ControllerBase
    {
        [HttpPost]
        public IActionResult GenerateInvoice()
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

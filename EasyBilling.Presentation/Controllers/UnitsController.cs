using EasyBilling.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UnitsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetSupportedUnits()
        {
            return Ok(UnitOfMeasure.Supported);
        }
    }
}

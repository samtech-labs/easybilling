using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Requests;
using EasyBilling.Presentation.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyBilling.Presentation.Controllers
{
    [Route("api/bank-accounts")]
    [ApiController]
    [Authorize(Policy = Policies.HasActiveMembership)]
    public class BankAccountController(IBankAccountService bankAccountService) : ControllerBase
    {
        private readonly IBankAccountService _bankAccountService = bankAccountService;

        [HttpGet]
        public async Task<IActionResult> GetByCompany(
            [FromQuery] Guid companyId,
            CancellationToken cancellationToken)
        {
            try
            {
                var accounts = await _bankAccountService.GetByCompanyAsync(companyId, cancellationToken);
                return Ok(accounts);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var account = await _bankAccountService.GetByIdAsync(id, cancellationToken);
                return Ok(account);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromQuery] Guid companyId,
            [FromBody] CreateBankAccountRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var account = await _bankAccountService.CreateAsync(companyId, request, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }) });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateBankAccountRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var account = await _bankAccountService.UpdateAsync(id, request, cancellationToken);
                return Ok(account);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }) });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _bankAccountService.DeleteAsync(id, cancellationToken);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}

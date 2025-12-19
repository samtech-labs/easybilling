using EasyBilling.Application.Interfaces;
using EasyBilling.Application.IServices;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Services
{
    public class CompanyService(ICompanyRepository companyRepository) : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository = companyRepository;

        public async Task<List<Company>> GetCompaniesByUserAsync(Guid userId)
        {
            return await _companyRepository.GetAllCompaniesByUserAsync(userId);
        }
    }
}

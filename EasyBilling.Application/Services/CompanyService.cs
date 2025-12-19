using EasyBilling.Application.Interfaces;
using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;
using EasyBilling.Application.Dtos;

namespace EasyBilling.Application.Services
{
    public class CompanyService(ICompanyRepository companyRepository, ICurrentUserService currentUserService) : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository = companyRepository;
        private readonly ICurrentUserService _currentUserService = currentUserService;

        public async Task<List<Company>> GetCompaniesByUserAsync(Guid userId)
        {
            return await _companyRepository.GetAllCompaniesByUserAsync(userId);
        }

        public async Task<CompanyResponseDto> GetCompanyDetailsFromAnaf(string cui)
        {
            var cleanCui = cui.Replace("RO", "").Replace(" ", "").Trim();
            var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

            if (anafDetails == null)
            {
                throw new InvalidOperationException($"No company details found for CUI '{cui}' in ANAF.");
            }

            var companyResponse = new CompanyResponseDto
            {
                Name = anafDetails.Name,
                CUI = cleanCui,
                Address = anafDetails.RegisteredAddress?.FormattedAddress,
                County = anafDetails.RegisteredAddress?.County,
                RegNumber = anafDetails.RegistrationNumber
            };

            return companyResponse;
        }

        public async Task<Company> CreateCompanyAsync(CreateCompanyRequest createCompanyRequest)
        {
            if (_currentUserService.UserId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            var cleanCui = createCompanyRequest.CUI.Replace("RO", "").Replace(" ", "").Trim();

            var existingCompany = await _companyRepository.GetByCuiAsync(cleanCui, _currentUserService.UserId);
            if (existingCompany != null)
            {
                throw new InvalidOperationException($"A company with CUI '{createCompanyRequest.CUI}' already exists.");
            }

            var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

            // We need to establish if we prefer ANAF data over user-provided data.
            // For the moment, we will prioritize ANAF data when available, but still allow user overrides for certain fields.

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = anafDetails?.Name ?? createCompanyRequest.Name,
                CUI = cleanCui,
                Address = anafDetails?.RegisteredAddress?.FormattedAddress ?? createCompanyRequest.Address,
                County = anafDetails?.RegisteredAddress?.County ?? createCompanyRequest.County,
                RegNumber = anafDetails?.RegistrationNumber ?? createCompanyRequest.RegNumber,
                IBAN = createCompanyRequest.IBAN,
                Bank = createCompanyRequest.Bank,
                UserId = _currentUserService.UserId
            };

            await _companyRepository.AddAsync(company);
            return company;
        }
    }
}

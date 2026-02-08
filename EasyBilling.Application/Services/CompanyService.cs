using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;
using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Interfaces.Repositories;

namespace EasyBilling.Application.Services
{
    public class CompanyService(ICompanyRepository companyRepository, UserContext userContext) : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository = companyRepository;
        private readonly UserContext _userContext = userContext;

        private const int MaxCompaniesPerUser = 3;

        public async Task<List<Company>> GetCompaniesByUserAsync(Guid userId)
        {
            return await _companyRepository.GetAllCompaniesByUserAsync(userId);
        }

        public async Task<List<Company>> GetAllCompaniesAsync(CancellationToken cancellationToken = default)
        {
            return await _companyRepository.GetAllCompaniesAsync(cancellationToken);
        }

        public async Task<PaginatedResult<Company>> GetCompaniesByUserPaginatedAsync(
            Guid userId,
            CompanyPaginationFilter filter)
        {
            return await _companyRepository.GetCompaniesByUserPaginatedAsync(userId, filter);
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
                City = anafDetails.RegisteredAddress?.City,
                Country = anafDetails.RegisteredAddress?.Country,
                RegNumber = anafDetails.RegistrationNumber,
                IsVatPayer = anafDetails.IsVatPayer,
                IsEFacturaActive = anafDetails.IsEFacturaActive
            };

            return companyResponse;
        }

        public async Task<Company> CreateCompanyAsync(CreateCompanyRequest createCompanyRequest)
        {
            if (_userContext.UserId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            // Check company limit per user
            var userCompanies = await _companyRepository.GetAllCompaniesByUserAsync(_userContext.UserId);
            if (userCompanies.Count >= MaxCompaniesPerUser)
            {
                throw new InvalidOperationException($"You have reached the maximum limit of {MaxCompaniesPerUser} companies.");
            }


            // move this to a helper function. it alreadys exists in multiple places
            var cleanCui = createCompanyRequest.CUI.Replace("RO", "").Replace(" ", "").Trim();

            var existingCompany = await _companyRepository.GetByCuiAsync(cleanCui, _userContext.UserId);
            if (existingCompany != null)
            {
                throw new InvalidOperationException($"A company with CUI '{createCompanyRequest.CUI}' already exists.");
            }

            var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = anafDetails?.Name ?? createCompanyRequest.Name,
                CUI = cleanCui,
                Address = anafDetails?.RegisteredAddress?.FormattedAddress ?? createCompanyRequest.Address,
                County = anafDetails?.RegisteredAddress?.County ?? createCompanyRequest.County,
                City = anafDetails?.RegisteredAddress?.City ?? createCompanyRequest.City,
                Country = anafDetails?.RegisteredAddress?.Country ?? createCompanyRequest.Country,
                RegNumber = anafDetails?.RegistrationNumber ?? createCompanyRequest.RegNumber,
                IBAN = createCompanyRequest.IBAN,
                Bank = createCompanyRequest.Bank,
                IsVatPayer = createCompanyRequest.IsVatPayer ?? anafDetails?.IsVatPayer ?? false,
                IsEFacturaActive = createCompanyRequest.IsEFacturaActive ?? anafDetails?.IsEFacturaActive ?? false,
                UserId = _userContext.UserId
            };

            await _companyRepository.AddAsync(company);
            return company;
        }

        public async Task<Company> CreateCompanyForUserAsync(CreateCompanyForUserRequest createCompanyForUserRequest)
        {
            // This method can only be called by ADMINs (enforced by controller authorization)
            // Validate that the target user exists
            var targetUserId = createCompanyForUserRequest.UserId;

            // Check company limit per user
            var userCompanies = await _companyRepository.GetAllCompaniesByUserAsync(targetUserId);
            if (userCompanies.Count >= MaxCompaniesPerUser)
            {
                throw new InvalidOperationException($"The user has reached the maximum limit of {MaxCompaniesPerUser} companies.");
            }

            var cleanCui = createCompanyForUserRequest.CUI.Replace("RO", "").Replace(" ", "").Trim();

            // Check if company with this CUI already exists for this user
            var existingCompany = await _companyRepository.GetByCuiAsync(cleanCui, targetUserId);
            if (existingCompany != null)
            {
                throw new InvalidOperationException($"A company with CUI '{createCompanyForUserRequest.CUI}' already exists for this user.");
            }

            // Fetch ANAF details
            var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

            // Prioritize ANAF data when available, but allow user overrides for certain fields
            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = anafDetails?.Name ?? createCompanyForUserRequest.Name,
                CUI = cleanCui,
                Address = anafDetails?.RegisteredAddress?.FormattedAddress ?? createCompanyForUserRequest.Address,
                County = anafDetails?.RegisteredAddress?.County ?? createCompanyForUserRequest.County,
                City = anafDetails?.RegisteredAddress?.City ?? createCompanyForUserRequest.City,
                Country = anafDetails?.RegisteredAddress?.Country ?? createCompanyForUserRequest.Country,
                RegNumber = anafDetails?.RegistrationNumber ?? createCompanyForUserRequest.RegNumber,
                IBAN = createCompanyForUserRequest.IBAN,
                Bank = createCompanyForUserRequest.Bank,
                IsVatPayer = createCompanyForUserRequest.IsVatPayer ?? anafDetails?.IsVatPayer ?? false,
                IsEFacturaActive = createCompanyForUserRequest.IsEFacturaActive ?? anafDetails?.IsEFacturaActive ?? false,
                UserId = targetUserId // Assign to the specified user
            };

            await _companyRepository.AddAsync(company);
            return company;
        }

        public async Task<Company?> GetCompanyByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            return await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        }

        public async Task DeleteCompanyAsync(Guid companyId)
        {
            var company = await _companyRepository.GetByIdAsync(companyId);

            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            if (company.UserId != _userContext.UserId)
            {
                throw new InvalidOperationException("Company does not belong to the current user.");
            }

            await _companyRepository.DeleteAsync(company);
        }

        public async Task DeleteCompanyForUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);

            if (company == null)
            {
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            if (company.UserId != userId)
            {
                throw new InvalidOperationException("Company does not belong to the specified user.");
            }

            await _companyRepository.DeleteAsync(company, cancellationToken);
        }

        public async Task<Company?> GetCompanyByCifAsync(string cif, CancellationToken cancellationToken = default)
        {
            var cleanCif = cif.Replace("RO", "").Replace(" ", "").Trim();
            return await _companyRepository.GetByCuiAsync(cleanCif, _userContext.UserId, cancellationToken);
        }
    }
}

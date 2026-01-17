using EasyBilling.Application.Requests;
using EasyBilling.Domain.Models;
using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces.Services;
using EasyBilling.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Application.Services;

public class CompanyService(
    ICompanyRepository companyRepository,
    UserContext userContext,
    ILogger<CompanyService> logger) : ICompanyService
{
    private readonly ICompanyRepository _companyRepository = companyRepository;
    private readonly UserContext _userContext = userContext;
    private readonly ILogger<CompanyService> _logger = logger;

    private const int MaxCompaniesPerUser = 3;

    public async Task<List<Company>> GetCompaniesByUserAsync(Guid userId)
    {
        _logger.LogInformation("Retrieving all companies for user {UserId}", userId);

        try
        {
            var companies = await _companyRepository.GetAllCompaniesByUserAsync(userId);

            _logger.LogInformation("Retrieved {CompanyCount} companies for user {UserId}",
                companies.Count, userId);

            return companies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving companies for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PaginatedResult<Company>> GetCompaniesByUserPaginatedAsync(
        Guid userId,
        CompanyPaginationFilter filter)
    {
        _logger.LogInformation("Retrieving paginated companies for user {UserId} - Page: {PageNumber}, PageSize: {PageSize}",
            userId, filter.PageNumber, filter.PageSize);

        try
        {
            _logger.LogDebug("Applying pagination filters - SearchTerm: {SearchTerm}, SortBy: {SortBy}",
                filter.SearchTerm ?? "none", filter.SortBy ?? "none");

            var result = await _companyRepository.GetCompaniesByUserPaginatedAsync(userId, filter);

            _logger.LogInformation("Retrieved {ItemCount} companies out of {TotalCount} for user {UserId} (page {PageNumber})",
                result.Items.Count, result.TotalCount, userId, result.PageNumber);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated companies for user {UserId}", userId);
            throw;
        }
    }

    public async Task<CompanyResponseDto> GetCompanyDetailsFromAnaf(string cui)
    {
        _logger.LogInformation("Fetching company details from ANAF for CUI: {CUI}", cui);

        try
        {
            var cleanCui = cui.Replace("RO", "").Replace(" ", "").Trim();

            _logger.LogDebug("Cleaned CUI: {CleanCUI} (original: {OriginalCUI})", cleanCui, cui);

            var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

            if (anafDetails == null)
            {
                _logger.LogWarning("No company details found in ANAF for CUI '{CUI}'", cleanCui);
                throw new InvalidOperationException($"No company details found for CUI '{cui}' in ANAF.");
            }

            _logger.LogInformation("Successfully retrieved ANAF details for CUI {CUI}: {CompanyName}",
                cleanCui, anafDetails.Name);

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

            _logger.LogDebug("Mapped ANAF response to CompanyResponseDto - Name: {Name}, IsVatPayer: {IsVatPayer}, IsEFacturaActive: {IsEFacturaActive}",
                companyResponse.Name, companyResponse.IsVatPayer, companyResponse.IsEFacturaActive);

            return companyResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching company details from ANAF for CUI: {CUI}", cui);
            throw;
        }
    }

    public async Task<Company> CreateCompanyAsync(CreateCompanyRequest createCompanyRequest)
    {
        _logger.LogInformation("Creating new company - CUI: {CUI}, Name: {Name}",
            createCompanyRequest.CUI, createCompanyRequest.Name);

        try
        {
            if (_userContext.UserId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to create company but user is not authenticated");
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            _logger.LogDebug("User authenticated - UserId: {UserId}", _userContext.UserId);

            var userCompanies = await _companyRepository.GetAllCompaniesByUserAsync(_userContext.UserId);

            _logger.LogDebug("User {UserId} currently has {CompanyCount} companies (max: {MaxCompanies})",
                _userContext.UserId, userCompanies.Count, MaxCompaniesPerUser);

            if (userCompanies.Count >= MaxCompaniesPerUser)
            {
                _logger.LogWarning("User {UserId} has reached maximum company limit ({MaxCompanies})",
                    _userContext.UserId, MaxCompaniesPerUser);
                throw new InvalidOperationException($"You have reached the maximum limit of {MaxCompaniesPerUser} companies.");
            }

            var cleanCui = createCompanyRequest.CUI.Replace("RO", "").Replace(" ", "").Trim();

            _logger.LogDebug("Cleaned CUI: {CleanCUI} (original: {OriginalCUI})", cleanCui, createCompanyRequest.CUI);

            var existingCompany = await _companyRepository.GetByCuiAsync(cleanCui, _userContext.UserId);
            if (existingCompany != null)
            {
                _logger.LogWarning("Company with CUI '{CUI}' already exists for user {UserId}",
                    cleanCui, _userContext.UserId);
                throw new InvalidOperationException($"A company with CUI '{createCompanyRequest.CUI}' already exists.");
            }

            _logger.LogDebug("Fetching ANAF details for CUI: {CUI}", cleanCui);

            var anafDetails = await ANAFIntegration.ANAFIntegration.GetCompanyDetails(cleanCui, DateTime.Today);

            if (anafDetails != null)
            {
                _logger.LogInformation("ANAF details retrieved for CUI {CUI}: {CompanyName}, IsVatPayer: {IsVatPayer}, IsEFacturaActive: {IsEFacturaActive}",
                    cleanCui, anafDetails.Name, anafDetails.IsVatPayer, anafDetails.IsEFacturaActive);
            }
            else
            {
                _logger.LogWarning("No ANAF details found for CUI {CUI}, using provided request information",
                    cleanCui);
            }

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

            _logger.LogDebug("Creating company entity: CompanyId: {CompanyId}, Name: {Name}, CUI: {CUI}, City: {City}, County: {County}",
                company.Id, company.Name, company.CUI, company.City, company.County);

            await _companyRepository.AddAsync(company);

            _logger.LogInformation("Company successfully created: {CompanyId} - {CompanyName} (CUI: {CUI}) for user {UserId}",
                company.Id, company.Name, company.CUI, _userContext.UserId);

            return company;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company - CUI: {CUI}, Name: {Name}",
                createCompanyRequest.CUI, createCompanyRequest.Name);
            throw;
        }
    }

    public async Task<Company?> GetCompanyByIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving company by ID: {CompanyId}", companyId);

        try
        {
            var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);

            if (company == null)
            {
                _logger.LogWarning("Company with ID '{CompanyId}' not found", companyId);
            }
            else
            {
                _logger.LogDebug("Company retrieved: {CompanyName} (CUI: {CUI})", company.Name, company.CUI);
            }

            return company;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task DeleteCompanyAsync(Guid companyId)
    {
        _logger.LogInformation("Deleting company {CompanyId}", companyId);

        try
        {
            var company = await _companyRepository.GetByIdAsync(companyId);

            if (company == null)
            {
                _logger.LogWarning("Company with ID '{CompanyId}' not found", companyId);
                throw new InvalidOperationException($"Company with ID '{companyId}' does not exist.");
            }

            _logger.LogDebug("Company found: {CompanyName} (CUI: {CUI}) belonging to user {UserId}",
                company.Name, company.CUI, company.UserId);

            if (company.UserId != _userContext.UserId)
            {
                _logger.LogWarning("Attempted to delete company {CompanyId} ({CompanyName}) that belongs to user {CompanyOwnerUserId}, but current user is {CurrentUserId}",
                    companyId, company.Name, company.UserId, _userContext.UserId);
                throw new InvalidOperationException("Company does not belong to the current user.");
            }

            await _companyRepository.DeleteAsync(company);

            _logger.LogInformation("Company successfully deleted: {CompanyId} - {CompanyName} (CUI: {CUI}) for user {UserId}",
                companyId, company.Name, company.CUI, _userContext.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<Company?> GetCompanyByCifAsync(string cif, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving company by CIF: {CIF}", cif);

        try
        {
            var cleanCif = cif.Replace("RO", "").Replace(" ", "").Trim();

            _logger.LogDebug("Cleaned CIF: {CleanCIF} (original: {OriginalCIF})", cleanCif, cif);

            var company = await _companyRepository.GetByCuiAsync(cleanCif, _userContext.UserId, cancellationToken);

            if (company == null)
            {
                _logger.LogWarning("Company with CIF '{CIF}' not found for user {UserId}", cleanCif, _userContext.UserId);
            }
            else
            {
                _logger.LogDebug("Company retrieved: {CompanyName} (CUI: {CUI}) for user {UserId}",
                    company.Name, company.CUI, _userContext.UserId);
            }

            return company;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company by CIF: {CIF}", cif);
            throw;
        }
    }
}
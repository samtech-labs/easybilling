using EasyBilling.Infrastructure.Persistence;
using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using EasyBilling.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace EasyBilling.Infrastructure.Repositories;

public class AnafTokenRepoistory(AppDbContext db, ILogger<AnafTokenRepoistory> logger) : IAnafTokenRepository
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<AnafTokenRepoistory> _logger = logger;

    public async Task AddAsync(AnafToken anafToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding new ANAF token for user {UserId}, expires at {ExpiresAt}",
            anafToken.UserId, anafToken.AccessTokenExpiresAt);

        try
        {
            if (anafToken == null)
            {
                _logger.LogWarning("Attempted to add null ANAF token");
                throw new ArgumentNullException(nameof(anafToken), "ANAF token cannot be null");
            }

            if (anafToken.UserId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to add ANAF token with empty UserId");
                throw new ArgumentException("UserId cannot be empty", nameof(anafToken));
            }

            _logger.LogDebug("ANAF token details - UserId: {UserId}, AccessTokenExpiresAt: {AccessTokenExpiresAt}, RefreshTokenExpiresAt: {RefreshTokenExpiresAt}",
                anafToken.UserId, anafToken.AccessTokenExpiresAt, anafToken.RefreshTokenExpiresAt);

            await _db.AnafTokens.AddAsync(anafToken, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("ANAF token successfully added to database for user {UserId}",
                anafToken.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding ANAF token for user {UserId}",
                anafToken?.UserId);
            throw;
        }
    }

    public async Task UpdateAsync(AnafToken anafToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating ANAF token for user {UserId}, new expiration: {ExpiresAt}",
            anafToken.UserId, anafToken.AccessTokenExpiresAt);

        try
        {
            if (anafToken == null)
            {
                _logger.LogWarning("Attempted to update null ANAF token");
                throw new ArgumentNullException(nameof(anafToken), "ANAF token cannot be null");
            }

            if (anafToken.UserId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to update ANAF token with empty UserId");
                throw new ArgumentException("UserId cannot be empty", nameof(anafToken));
            }

            _logger.LogDebug("Updating ANAF token - UserId: {UserId}, AccessTokenExpiresAt: {AccessTokenExpiresAt}, RefreshTokenExpiresAt: {RefreshTokenExpiresAt}",
                anafToken.UserId, anafToken.AccessTokenExpiresAt, anafToken.RefreshTokenExpiresAt);

            _db.AnafTokens.Update(anafToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("ANAF token successfully updated for user {UserId}",
                anafToken.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating ANAF token for user {UserId}",
                anafToken?.UserId);
            throw;
        }
    }

    public async Task<AnafToken?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving ANAF token for user {UserId}", userId);

        try
        {
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve ANAF token with empty UserId");
                return null;
            }

            var token = await _db.AnafTokens
                .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

            if (token == null)
            {
                _logger.LogWarning("No ANAF token found for user {UserId}", userId);
                return null;
            }

            var isExpired = token.AccessTokenExpiresAt <= DateTime.UtcNow;

            _logger.LogInformation("ANAF token retrieved for user {UserId} - Status: {Status}, ExpiresAt: {ExpiresAt}",
                userId, isExpired ? "Expired" : "Valid", token.AccessTokenExpiresAt);

            _logger.LogDebug("ANAF token details - CreatedAt: {CreatedAt}, RefreshTokenExpiresAt: {RefreshTokenExpiresAt}",
                token.CreatedAt, token.RefreshTokenExpiresAt);

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ANAF token for user {UserId}", userId);
            throw;
        }
    }
}

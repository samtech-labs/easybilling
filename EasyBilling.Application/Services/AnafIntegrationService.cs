using EasyBilling.Application.Dtos;
using EasyBilling.Application.Interfaces;
using EasyBilling.Domain.Models;

namespace EasyBilling.Application.Services
{
    public class AnafIntegrationService(IAnafTokenRepository anafTokenRepository) : IAnafIntegrationService
    {
        private readonly IAnafTokenRepository _anafTokenRepository = anafTokenRepository;

        public async Task SaveAnafTokenAsync(AnafTokenCreateDto anafTokenCreateDto)
        {
            var anafToken = new AnafToken
            {
                UserId = anafTokenCreateDto.UserId,
                AccessToken = anafTokenCreateDto.AccessToken,
                RefreshToken = anafTokenCreateDto.RefreshToken,
                AccessTokenExpiresAt = anafTokenCreateDto.AccessTokenExpiresAt,
                RefreshTokenExpiresAt = anafTokenCreateDto.RefreshTokenExpiresAt,
                ExpiresAt = anafTokenCreateDto.ExpiresAt,
                CreatedAt = anafTokenCreateDto.CreatedAt
            };

            await _anafTokenRepository.AddAsync(anafToken);
        }

        public async Task UpdateAnafTokenAsync(AnafToken anafToken)
        {
            await _anafTokenRepository.UpdateAsync(anafToken);
        }

        public async Task<AnafToken?> GetAnafTokenByUserIdAsync(Guid userId)
        {
            return await _anafTokenRepository.GetByUserIdAsync(userId);
        }

        public async Task<bool> IsTokenExpiredAsync(Guid userId)
        {
            var anafToken = await _anafTokenRepository.GetByUserIdAsync(userId);

            if (anafToken == null)
            {
                return true;
            }

            if (anafToken.AccessTokenExpiresAt <= DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }
    }
}

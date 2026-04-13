using System.Security.Cryptography;
using EasyBilling.Application.Interfaces.Services;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace EasyBilling.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 128 / 8;
    private const int HashSize = 256 / 8;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: HashSize);

        var combined = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, combined, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, combined, SaltSize, HashSize);

        return Convert.ToBase64String(combined);
    }

    public bool Verify(string password, string hashedPassword)
    {
        var combined = Convert.FromBase64String(hashedPassword);
        if (combined.Length != SaltSize + HashSize)
            return false;

        var salt = new byte[SaltSize];
        Buffer.BlockCopy(combined, 0, salt, 0, SaltSize);

        var expectedHash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: HashSize);

        return CryptographicOperations.FixedTimeEquals(
            combined.AsSpan(SaltSize),
            expectedHash);
    }
}

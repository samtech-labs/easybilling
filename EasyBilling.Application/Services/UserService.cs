using System.Text;
using EasyBilling.Application.Interfaces;

namespace EasyBilling.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }
    public async Task<bool> DecodeToken(string authHeader)
    {
        if (string.IsNullOrWhiteSpace(authHeader) || 
            !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        
        string base64Part = authHeader["Basic ".Length..].Trim();
        string decoded;

        try
        {
            var bytes = Convert.FromBase64String(base64Part);
            decoded = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return false; 
        }

        var parts = decoded.Split(':', 2);
        if (parts.Length != 2)
            return false;

        string clientIdString = parts[0];
        string clientSecret = parts[1];

        Guid clientId;
        bool isGuid = Guid.TryParse(parts[0], out clientId);

        if (!isGuid) return false;


        var user = await _repository.GetByClientId(clientIdString, clientSecret);
        if (user is null) return false;

        if (user.Client_Secret != clientSecret) return false;

        return true;
    }
}
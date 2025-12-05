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
}
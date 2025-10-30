using Microsoft.Extensions.DependencyInjection;
using EasyBilling.Infrastructure.Persistence.Repositories;
using EasyBilling.Application.Interfaces;

namespace EasyBilling.Infrastructure.Persistence
{
    public static class PersistenceServiceRegistration
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services)
        {
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EasyBilling.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Dummy connection string for design-time operations (migrations bundle creation).
        // EF Core only needs to understand the model schema - it doesn't connect to the DB.
        // At runtime, the actual connection string is passed via --connection argument.
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=easybilling-dev;Username=postgres;Password=postgres;");

        return new AppDbContext(optionsBuilder.Options);
    }
}

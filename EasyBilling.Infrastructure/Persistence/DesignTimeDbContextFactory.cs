using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EasyBilling.Infrastructure.Persistence
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString = GetArg(args, "--connection") ?? 
                Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING") ?? 
                throw new InvalidOperationException("Missing connection string. Pass --connection or set POSTGRES_CONNECTION_STRING.");
            
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new AppDbContext(optionsBuilder);
        }

        private static string? GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];

            return null;
        }
    }
}

using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;


namespace EasyBilling.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Company> Companies { get; set; }
    }
}

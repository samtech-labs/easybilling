using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Persistence
{
    public class AppDbContext
        : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);


            builder.Entity<ApplicationUser>()
                .OwnsOne(u => u.Address);


            builder.Entity<Company>()
                .OwnsOne(c => c.Address);

            builder.Entity<Company>()
                .HasOne<ApplicationUser>()
                .WithMany(u => u.ClientCompanies)
                .HasForeignKey(c => c.ContractorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Company>()
                .HasIndex(c => new { c.ContractorId, c.TaxId });
        }

        public DbSet<Company> Companies => Set<Company>();

    }
}

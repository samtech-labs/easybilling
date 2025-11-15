using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;


namespace EasyBilling.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Company> Companies { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Company>().HasData(
                new Company
                {
                    Id = Guid.Parse("e11e24c2-8c61-4adb-af89-9464ac44964a"),
                    Name = "SAMTECH LABS SRL",
                    CUI = "RO49311115",
                    RegNumber = "J18/1171/2023",
                    Address = "Strada 14 Octombrie 115B, Targu Jiu, Gorj",
                    IBAN = "RO49AAAA1B31007593840000",
                    Bank = "Revolut Bank UAD"
                },
                new Company
                {
                    Id = Guid.Parse("40019908-3df7-4764-bbb7-1776e8e23245"),
                    Name = "Demo Client SRL",
                    CUI = "RO87654321",
                    RegNumber = "J12/567/2020",
                    Address = "Str. Testului 2, Cluj-Napoca, Romania",
                    IBAN = "RO49BBBB1B31007593840000",
                    Bank = "Banca Transilvania"
                }
            );
        }
    }
}

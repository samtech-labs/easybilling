using EasyBilling.Domain.Models;
using Microsoft.EntityFrameworkCore;


namespace EasyBilling.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Company> Companies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Client> Clients { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = Guid.Parse("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101"),
                    Username = "admin",
                    Email = "admin@gmail.com",
                    Client_Id = Guid.Parse("e8c9d14f-3df0-4ab5-9a72-6c1f4bb3a202"),
                    Client_Secret = "admin123"
                },
                new User
                {
                    Id = Guid.Parse("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303"),
                    Username = "user",
                    Email = "user@gmail.com",
                    Client_Id = Guid.Parse("c2a4f8b1-6e5d-4f17-91bb-0f92b74f4404"),
                    Client_Secret = "user123"
                }
            );

            modelBuilder.Entity<Client>().HasData(
                new Client
                {
                    Id = Guid.Parse("a3f5d9b2-1e34-4d5c-92a4-1d9c4c7b0151"),
                    Name = "SC Spectacol SRL",
                    Address = "Targu-Jiu, str. Spectaculosilor 14",
                    Company_Id = Guid.Parse("c13dbb54-9fc5-4c72-92df-c47e6dfcce21"),
                    Bank = "BCR",
                    CUI = "RO12345678",
                    IBAN = "RO49BCRL00001012345678",
                    RegNumber = "J40/1234/2010"
                },
                new Client
                {
                    Id = Guid.Parse("b7c89fa1-6bd2-4c26-a7ea-3b2cdb0f9e62"),
                    Name = "Pandurii Tismana",
                    Address = "Tismana",
                    Company_Id = Guid.Parse("de45bb29-fb3f-4c53-b9a0-87d13a6cc920"),
                    Bank = "BT",
                    CUI = "RO27833491",
                    IBAN = "RO27BTRL0000123456789012",
                    RegNumber = "J12/567/2015"
                }
            );
        }
    }
}

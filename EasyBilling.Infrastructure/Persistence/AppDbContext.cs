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
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceBlob> InvoiceBlobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Companies)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Company>()
                .HasMany(c => c.Clients)
                .WithOne(cl => cl.Company)
                .HasForeignKey(cl => cl.CompanyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Invoice>()
                .HasMany(i => i.InvoiceLines)
                .WithOne(il => il.Invoice)
                .HasForeignKey(il => il.InvoiceId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<InvoiceBlob>(e =>
            {
                e.HasKey(x => x.InvoiceId);

                e.Property(x => x.ContainerName).IsRequired();
                e.Property(x => x.BlobName).IsRequired();

                e.HasOne(x => x.Invoice)
                    .WithOne() 
                    .HasForeignKey<InvoiceBlob>(x => x.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = Guid.Parse("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101"),
                    Username = "admin",
                    Password = "admin123",
                    Email = "admin@gmail.com"
                },
                new User
                {
                    Id = Guid.Parse("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303"),
                    Username = "user",
                    Password = "user123",
                    Email = "user@gmail.com"
                }
            );

            modelBuilder.Entity<Company>().HasData(
                new Company
                {
                    Id = Guid.Parse("e11e24c2-8c61-4adb-af89-9464ac44964a"),
                    Name = "SAMTECH LABS SRL",
                    CUI = "RO49311115",
                    RegNumber = "J18/1171/2023",
                    Address = "Strada 14 Octombrie 115B, Targu Jiu, Gorj",
                    IBAN = "RO49AAAA1B31007593840000",
                    Bank = "Revolut Bank UAD",
                    UserId = Guid.Parse("a3f1b2c6-5d7a-4c89-bc36-9e7f2a51d101")
                },
                new Company
                {
                    Id = Guid.Parse("40019908-3df7-4764-bbb7-1776e8e23245"),
                    Name = "Demo Client SRL",
                    CUI = "RO87654321",
                    RegNumber = "J12/567/2020",
                    Address = "Str. Testului 2, Cluj-Napoca, Romania",
                    IBAN = "RO49BBBB1B31007593840000",
                    Bank = "Banca Transilvania",
                    UserId = Guid.Parse("5b7d8e03-9f3e-4c28-ae10-2a6f7c934303")
                }
            );
        }
    }
}
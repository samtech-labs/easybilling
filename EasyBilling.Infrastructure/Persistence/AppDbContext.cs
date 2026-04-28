using EasyBilling.Domain.Models;
using EasyBilling.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EasyBilling.Infrastructure.Persistence
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Company> Companies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<AnafToken> AnafTokens { get; set; }
        public DbSet<InvoiceAnafSubmission> InvoiceAnafSubmissions { get; set; }
        public DbSet<MembershipType> MembershipTypes { get; set; }
        public DbSet<Membership> Memberships { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Companies)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.AnafToken)
                .WithOne(t => t.User)
                .HasForeignKey<AnafToken>(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Membership)
                .WithOne(m => m.User)
                .HasForeignKey<Membership>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Company>()
                .HasMany(c => c.Clients)
                .WithOne(cl => cl.Company)
                .HasForeignKey(cl => cl.CompanyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoiceLine>(entity =>
            {
                entity.Property(il => il.Unit)
                    .HasDefaultValue(UnitOfMeasure.Default)
                    .HasMaxLength(UnitOfMeasure.MaxLength);
            });

            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasMany(i => i.InvoiceLines)
                    .WithOne(il => il.Invoice)
                    .HasForeignKey(il => il.InvoiceId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Type)
                    .HasConversion<int>()
                    .HasDefaultValue(InvoiceType.Invoice);

                entity.Property(e => e.Currency)
                    .HasConversion<int>()
                    .HasDefaultValue(Currency.RON);

                entity.HasOne(e => e.OriginalInvoice)
                    .WithMany(e => e.CreditNotes)
                    .HasForeignKey(e => e.OriginalInvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.OriginalInvoiceId);
                entity.HasIndex(e => e.Type);
            });

            modelBuilder.Entity<InvoiceAnafSubmission>(entity =>
            {
                entity.ToTable("InvoiceAnafSubmissions");

                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Invoice)
                    .WithMany(i => i.AnafSubmissions)
                    .HasForeignKey(e => e.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.UploadIndex)
                    .HasMaxLength(100);

                entity.Property(e => e.DownloadId)
                    .HasMaxLength(100);

                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(2000);

                entity.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.HasIndex(e => e.InvoiceId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => new { e.Status, e.LastCheckedAt });
            });

            modelBuilder.Entity<MembershipType>(entity =>
            {
                entity.ToTable("MembershipTypes");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.Price)
                    .HasPrecision(18, 2);

                entity.HasIndex(e => e.Name)
                    .IsUnique();

                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<Membership>(entity =>
            {
                entity.ToTable("Memberships");

                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.MembershipType)
                    .WithMany()
                    .HasForeignKey(e => e.MembershipTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.UserId)
                    .IsUnique();

                entity.HasIndex(e => e.EndDate);
            });
        }
    }
}

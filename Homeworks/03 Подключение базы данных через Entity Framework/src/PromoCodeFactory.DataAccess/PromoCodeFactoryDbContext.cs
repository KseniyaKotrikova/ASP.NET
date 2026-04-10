using Microsoft.EntityFrameworkCore;
using PromoCodeFactory.Core.Domain.Administration;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;

namespace PromoCodeFactory.DataAccess;

public class PromoCodeFactoryDbContext : DbContext
{
    public PromoCodeFactoryDbContext(DbContextOptions<PromoCodeFactoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<PromoCode> PromoCodes { get; set; }
    public DbSet<Preference> Preferences { get; set; }
    public DbSet<Role> Roles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. Настройка Customer
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();

            // Связь Многие-ко-многим с Preference
            entity.HasMany(c => c.Preferences)
                .WithMany(p => p.Customers)
                .UsingEntity("CustomerPreference"); // Имя промежуточной таблицы
        });

        // 2. Настройка Preference
        modelBuilder.Entity<Preference>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        // 3. Настройка PromoCode
        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ServiceInfo).HasMaxLength(256);
            entity.Property(x => x.PartnerName).HasMaxLength(100);

            // Обязательная связь с менеджером (Employee)
            entity.HasOne(x => x.PartnerManager)
                .WithMany()
                .IsRequired();

            // Обязательная связь с предпочтением
            entity.HasOne(x => x.Preference)
                .WithMany()
                .IsRequired();
        });

        // 4. Настройка связующей таблицы CustomerPromoCode
        modelBuilder.Entity<CustomerPromoCode>(entity =>
        {
            // Составной первичный ключ
            entity.HasKey(x => new { x.CustomerId, x.PromoCodeId });

            // Связь с Customer (т.к. в Customer есть навигационное свойство CustomerPromoCodes)
            entity.HasOne<Customer>()
                .WithMany(c => c.CustomerPromoCodes)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Связь с PromoCode (т.к. в PromoCode есть навигационное свойство CustomerPromoCodes)
            entity.HasOne<PromoCode>()
                .WithMany(p => p.CustomerPromoCodes)
                .HasForeignKey(x => x.PromoCodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}

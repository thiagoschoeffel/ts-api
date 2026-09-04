using Microsoft.EntityFrameworkCore;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Production;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CatalogOffer> CatalogOffers => Set<CatalogOffer>();
    public DbSet<ProducibleItem> ProducibleItems => Set<ProducibleItem>();
    public DbSet<FrozenConfiguration> FrozenConfigurations => Set<FrozenConfiguration>();
    public DbSet<FrozenLot> FrozenLots => Set<FrozenLot>();
    public DbSet<FrozenStockMovement> FrozenStockMovements => Set<FrozenStockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<CatalogOffer>(configuration =>
        {
            configuration.ToTable("catalog_offers");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Name).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.NormalizedName).HasMaxLength(160).IsRequired();
            configuration.HasIndex(item => item.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<ProducibleItem>(configuration =>
        {
            configuration.ToTable("producible_items");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Name).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.NormalizedName).HasMaxLength(160).IsRequired();
            configuration.HasIndex(item => item.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<FrozenConfiguration>(configuration =>
        {
            configuration.ToTable("frozen_configurations");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Presentation).HasMaxLength(120).IsRequired();
            configuration.Property(item => item.QuantityPerUnit).HasPrecision(12, 3);
            configuration.Property(item => item.UnitPrice).HasPrecision(12, 2);
            configuration.HasIndex(item => new { item.OfferId, item.ProducibleItemId, item.Presentation }).IsUnique();
            configuration.HasOne<CatalogOffer>()
                .WithMany()
                .HasForeignKey(item => item.OfferId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<ProducibleItem>()
                .WithMany()
                .HasForeignKey(item => item.ProducibleItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FrozenLot>(configuration =>
        {
            configuration.ToTable("frozen_lots");
            configuration.HasKey(item => item.Id);
            configuration.Ignore(item => item.Balance);
            configuration.Ignore(item => item.Movements);
            configuration.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
            configuration.HasOne<FrozenConfiguration>()
                .WithMany()
                .HasForeignKey(item => item.FrozenConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasMany<FrozenStockMovement>("_movements")
                .WithOne()
                .HasForeignKey(item => item.FrozenLotId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Navigation("_movements").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.HasIndex(item => new { item.ExpiresOn, item.ManufacturedOn, item.Id });
            configuration.HasIndex(item => item.IdempotencyKey).IsUnique();
        });

        modelBuilder.Entity<FrozenStockMovement>(configuration =>
        {
            configuration.ToTable("frozen_stock_movements");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Origin).HasMaxLength(80).IsRequired();
            configuration.Property(item => item.Reason).HasMaxLength(500);
            configuration.Ignore(item => item.SignedQuantity);
            configuration.HasIndex(item => new { item.FrozenLotId, item.OccurredAt });
        });
    }
}

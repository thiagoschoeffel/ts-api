using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Organizations;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Production;

namespace Ts.Api.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IOrganizationContext organizationContext) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<PlatformUser> Users => Set<PlatformUser>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<CatalogOffer> CatalogOffers => Set<CatalogOffer>();
    public DbSet<ProducibleItem> ProducibleItems => Set<ProducibleItem>();
    public DbSet<FrozenConfiguration> FrozenConfigurations => Set<FrozenConfiguration>();
    public DbSet<FrozenLot> FrozenLots => Set<FrozenLot>();
    public DbSet<FrozenStockMovement> FrozenStockMovements => Set<FrozenStockMovement>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<FrozenStockAllocation> FrozenStockAllocations => Set<FrozenStockAllocation>();
    public DbSet<OrderCharge> OrderCharges => Set<OrderCharge>();
    public DbSet<DailyCapacity> DailyCapacities => Set<DailyCapacity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<Organization>(configuration =>
        {
            configuration.ToTable("organizations");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Name).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.Slug).HasMaxLength(100).IsRequired();
            configuration.HasIndex(item => item.Slug).IsUnique();
        });

        modelBuilder.Entity<PlatformUser>(configuration =>
        {
            configuration.ToTable("users");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.ExternalSubject).HasMaxLength(200).IsRequired();
            configuration.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            configuration.HasIndex(item => item.ExternalSubject).IsUnique();
        });

        modelBuilder.Entity<OrganizationMembership>(configuration =>
        {
            configuration.ToTable("organization_memberships");
            configuration.HasKey(item => new { item.OrganizationId, item.UserId });
            configuration.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<PlatformUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<CatalogOffer>(configuration =>
        {
            configuration.ToTable("catalog_offers");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Property(item => item.Name).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.NormalizedName).HasMaxLength(160).IsRequired();
            configuration.HasIndex(item => new { item.OrganizationId, item.NormalizedName }).IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<ProducibleItem>(configuration =>
        {
            configuration.ToTable("producible_items");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Property(item => item.Name).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.NormalizedName).HasMaxLength(160).IsRequired();
            configuration.HasIndex(item => new { item.OrganizationId, item.NormalizedName }).IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<FrozenConfiguration>(configuration =>
        {
            configuration.ToTable("frozen_configurations");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Property(item => item.Presentation).HasMaxLength(120).IsRequired();
            configuration.Property(item => item.QuantityPerUnit).HasPrecision(12, 3);
            configuration.Property(item => item.UnitPrice).HasPrecision(12, 2);
            configuration.HasIndex(item => new
            {
                item.OrganizationId,
                item.OfferId,
                item.ProducibleItemId,
                item.Presentation,
            }).IsUnique();
            configuration.HasOne<CatalogOffer>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.OfferId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<ProducibleItem>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.ProducibleItemId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<FrozenLot>(configuration =>
        {
            configuration.ToTable("frozen_lots");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Ignore(item => item.Balance);
            configuration.Ignore(item => item.Movements);
            configuration.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
            configuration.HasOne<FrozenConfiguration>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.FrozenConfigurationId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasMany<FrozenStockMovement>("_movements")
                .WithOne()
                .HasForeignKey(item => new { item.OrganizationId, item.FrozenLotId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Navigation("_movements").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.HasIndex(item => new
            {
                item.OrganizationId,
                item.ExpiresOn,
                item.ManufacturedOn,
                item.Id,
            });
            configuration.HasIndex(item => new { item.OrganizationId, item.IdempotencyKey }).IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<FrozenStockMovement>(configuration =>
        {
            configuration.ToTable("frozen_stock_movements");
            configuration.HasKey(item => item.Id);
            configuration.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Property(item => item.Origin).HasMaxLength(80).IsRequired();
            configuration.Property(item => item.Reason).HasMaxLength(500);
            configuration.Ignore(item => item.SignedQuantity);
            configuration.HasIndex(item => new { item.OrganizationId, item.FrozenLotId, item.OccurredAt });
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<Order>(configuration =>
        {
            configuration.ToTable("orders");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Property(item => item.ConfirmationIdempotencyKey).HasMaxLength(200);
            configuration.Property(item => item.Version).IsConcurrencyToken();
            configuration.Ignore(item => item.Items);
            configuration.Ignore(item => item.FrozenAllocations);
            configuration.Ignore(item => item.Charges);
            configuration.Ignore(item => item.DailyCapacityUnits);
            configuration.Ignore(item => item.TotalAmount);
            configuration.HasMany<OrderItem>("_items")
                .WithOne()
                .HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasMany<FrozenStockAllocation>("_frozenAllocations")
                .WithOne()
                .HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasMany<OrderCharge>("_charges")
                .WithOne()
                .HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Navigation("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.Navigation("_frozenAllocations").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.Navigation("_charges").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.HasIndex(item => new { item.OrganizationId, item.ConfirmationIdempotencyKey })
                .IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<OrderItem>(configuration =>
        {
            configuration.ToTable("order_items");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.Property(item => item.UnitPrice).HasPrecision(12, 2);
            configuration.Ignore(item => item.Total);
            configuration.HasOne<CatalogOffer>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.OfferId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<FrozenConfiguration>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.FrozenConfigurationId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<FrozenStockAllocation>(configuration =>
        {
            configuration.ToTable("frozen_stock_allocations");
            configuration.HasKey(item => item.Id);
            configuration.HasOne<OrderItem>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.OrderItemId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<FrozenConfiguration>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.FrozenConfigurationId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<FrozenLot>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.FrozenLotId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasIndex(item => new { item.OrganizationId, item.OrderItemId, item.FrozenLotId })
                .IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<OrderCharge>(configuration =>
        {
            configuration.ToTable("order_charges");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Amount).HasPrecision(12, 2);
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<DailyCapacity>(configuration =>
        {
            configuration.ToTable("daily_capacities");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.Ignore(item => item.AvailableUnits);
            configuration.HasIndex(item => new { item.OrganizationId, item.OperationalDate }).IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateTenantWrites()
    {
        var tenantEntries = ChangeTracker.Entries<ITenantOwned>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();

        if (tenantEntries.Length == 0)
        {
            return;
        }

        var organizationId = organizationContext.OrganizationId;
        foreach (var entry in tenantEntries)
        {
            if (entry.Entity.OrganizationId != organizationId)
            {
                throw new InvalidOperationException("Não é permitido gravar dados de outra organização.");
            }

            if (entry.State == EntityState.Modified
                && entry.Property(nameof(ITenantOwned.OrganizationId)).IsModified)
            {
                throw new InvalidOperationException("A organização de uma entidade não pode ser alterada.");
            }
        }
    }
}

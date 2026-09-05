using Microsoft.EntityFrameworkCore;
using Ts.Api.Application.Common;
using Ts.Api.Domain.Catalog;
using Ts.Api.Domain.Common;
using Ts.Api.Domain.Customers;
using Ts.Api.Domain.Finance;
using Ts.Api.Domain.FrozenStock;
using Ts.Api.Domain.Organizations;
using Ts.Api.Domain.Orders;
using Ts.Api.Domain.Plans;
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
    public DbSet<ProducibleComposition> ProducibleCompositions => Set<ProducibleComposition>();
    public DbSet<ProducibleComponent> ProducibleComponents => Set<ProducibleComponent>();
    public DbSet<CustomerDietaryRestriction> CustomerDietaryRestrictions => Set<CustomerDietaryRestriction>();
    public DbSet<PlanAcquisition> PlanAcquisitions => Set<PlanAcquisition>();
    public DbSet<PlanCreditMovement> PlanCreditMovements => Set<PlanCreditMovement>();
    public DbSet<FinancialCreditMovement> FinancialCreditMovements => Set<FinancialCreditMovement>();
    public DbSet<OrderItemComponent> OrderItemComponents => Set<OrderItemComponent>();
    public DbSet<OrderPlanCreditAllocation> OrderPlanCreditAllocations => Set<OrderPlanCreditAllocation>();
    public DbSet<OrderConfirmationAudit> OrderConfirmationAudits => Set<OrderConfirmationAudit>();
    public DbSet<OrderLifecycleEvent> OrderLifecycleEvents => Set<OrderLifecycleEvent>();

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
            configuration.Property(item => item.CreationIdempotencyKey).HasMaxLength(200).IsRequired();
            configuration.Property(item => item.LastModificationIdempotencyKey).HasMaxLength(200);
            configuration.Property(item => item.Version).IsConcurrencyToken();
            configuration.Ignore(item => item.Items);
            configuration.Ignore(item => item.FrozenAllocations);
            configuration.Ignore(item => item.Charges);
            configuration.Ignore(item => item.DailyCapacityUnits);
            configuration.Ignore(item => item.TotalAmount);
            configuration.Ignore(item => item.ComponentSnapshots);
            configuration.Ignore(item => item.PlanCreditAllocations);
            configuration.Ignore(item => item.ConfirmationAudits);
            configuration.Ignore(item => item.LifecycleEvents);
            configuration.HasMany<OrderItem>("_items")
                .WithOne()
                .HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Cascade);
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
            configuration.HasMany<OrderItemComponent>("_componentSnapshots")
                .WithOne().HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.HasMany<OrderPlanCreditAllocation>("_planCreditAllocations")
                .WithOne().HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.HasMany<OrderConfirmationAudit>("_confirmationAudits")
                .WithOne().HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.HasMany<OrderLifecycleEvent>("_lifecycleEvents")
                .WithOne().HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.Navigation("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.Navigation("_frozenAllocations").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.Navigation("_charges").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.Navigation("_componentSnapshots").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.Navigation("_planCreditAllocations").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.Navigation("_confirmationAudits").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.Navigation("_lifecycleEvents").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.HasIndex(item => new { item.OrganizationId, item.ConfirmationIdempotencyKey })
                .IsUnique();
            configuration.HasIndex(item => new { item.OrganizationId, item.CreationIdempotencyKey })
                .IsUnique();
            configuration.HasIndex(item => new { item.OrganizationId, item.LastModificationIdempotencyKey })
                .IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<OrderItem>(configuration =>
        {
            configuration.ToTable("order_items");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.Property(item => item.UnitPrice).HasPrecision(12, 2);
            configuration.Property(item => item.OfferName).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.ProducibleItemName).HasMaxLength(160);
            configuration.Property(item => item.FrozenPresentation).HasMaxLength(120);
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
            configuration.HasOne<ProducibleItem>()
                .WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.ProducibleItemId })
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
            configuration.Property(item => item.Version).IsConcurrencyToken();
            configuration.Property(item => item.LastConfigurationIdempotencyKey)
                .HasMaxLength(200)
                .IsRequired();
            configuration.HasIndex(item => new { item.OrganizationId, item.OperationalDate }).IsUnique();
            configuration.HasIndex(item => new
            {
                item.OrganizationId,
                item.LastConfigurationIdempotencyKey,
            }).IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        ConfigureConfirmationCommerce(modelBuilder);
    }

    private void ConfigureConfirmationCommerce(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProducibleComposition>(configuration =>
        {
            configuration.ToTable("producible_compositions");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.Ignore(item => item.Components);
            configuration.HasOne<ProducibleItem>().WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.ProducibleItemId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.HasMany<ProducibleComponent>("_components").WithOne()
                .HasForeignKey(item => new { item.OrganizationId, item.CompositionId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.Navigation("_components").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.HasIndex(item => new { item.OrganizationId, item.ProducibleItemId, item.Version }).IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<ProducibleComponent>(configuration =>
        {
            configuration.ToTable("producible_components");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Name).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.MeasurementUnit).HasMaxLength(30).IsRequired();
            configuration.Property(item => item.DietaryMarkers).HasMaxLength(500).IsRequired();
            configuration.Property(item => item.Quantity).HasPrecision(12, 3);
            configuration.Ignore(item => item.Markers);
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<CustomerDietaryRestriction>(configuration =>
        {
            configuration.ToTable("customer_dietary_restrictions");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Marker).HasMaxLength(80).IsRequired();
            configuration.HasOne<Organization>().WithMany().HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasIndex(item => new { item.OrganizationId, item.CustomerId, item.Marker }).IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<PlanAcquisition>(configuration =>
        {
            configuration.ToTable("plan_acquisitions");
            configuration.HasKey(item => item.Id);
            configuration.HasAlternateKey(item => new { item.OrganizationId, item.Id });
            configuration.Property(item => item.PlanName).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.BenefitAmountPerCredit).HasPrecision(12, 2);
            configuration.HasOne<Organization>().WithMany().HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<CatalogOffer>().WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.EligibleOfferId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.Ignore(item => item.Movements);
            configuration.Ignore(item => item.Balance);
            configuration.HasMany<PlanCreditMovement>("_movements").WithOne()
                .HasForeignKey(item => new { item.OrganizationId, item.AcquisitionId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.Navigation("_movements").UsePropertyAccessMode(PropertyAccessMode.Field);
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<PlanCreditMovement>(configuration =>
        {
            configuration.ToTable("plan_credit_movements");
            configuration.HasKey(item => item.Id);
            configuration.Ignore(item => item.SignedQuantity);
            configuration.HasIndex(item => new { item.OrganizationId, item.OrderId, item.OrderItemId });
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<FinancialCreditMovement>(configuration =>
        {
            configuration.ToTable("financial_credit_movements");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Amount).HasPrecision(12, 2);
            configuration.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            configuration.HasOne<Organization>().WithMany().HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<Order>().WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.OrderId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id })
                .OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            configuration.Ignore(item => item.SignedAmount);
            configuration.HasIndex(item => new { item.OrganizationId, item.CustomerId, item.OccurredAt });
            configuration.HasIndex(item => new { item.OrganizationId, item.OrderId })
                .IsUnique().HasFilter("\"OrderId\" IS NOT NULL AND \"Type\" = 1");
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<OrderItemComponent>(configuration =>
        {
            configuration.ToTable("order_item_components");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Name).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.MeasurementUnit).HasMaxLength(30).IsRequired();
            configuration.Property(item => item.DietaryMarkers).HasMaxLength(500).IsRequired();
            configuration.Property(item => item.QuantityPerUnit).HasPrecision(12, 3);
            configuration.Property(item => item.TotalQuantity).HasPrecision(12, 3);
            configuration.HasOne<OrderItem>().WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.OrderItemId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<OrderPlanCreditAllocation>(configuration =>
        {
            configuration.ToTable("order_plan_credit_allocations");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.PlanName).HasMaxLength(160).IsRequired();
            configuration.Property(item => item.CoveredAmount).HasPrecision(12, 2);
            configuration.HasOne<OrderItem>().WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.OrderItemId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.HasOne<PlanAcquisition>().WithMany()
                .HasForeignKey(item => new { item.OrganizationId, item.AcquisitionId })
                .HasPrincipalKey(item => new { item.OrganizationId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<OrderConfirmationAudit>(configuration =>
        {
            configuration.ToTable("order_confirmation_audits");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
            configuration.Property(item => item.DiscountReason).HasMaxLength(500);
            configuration.Property(item => item.Subtotal).HasPrecision(12, 2);
            configuration.Property(item => item.PlanCreditCoveredAmount).HasPrecision(12, 2);
            configuration.Property(item => item.DiscountAmount).HasPrecision(12, 2);
            configuration.Property(item => item.DeliveryFee).HasPrecision(12, 2);
            configuration.Property(item => item.FinancialCreditApplied).HasPrecision(12, 2);
            configuration.Property(item => item.AmountDue).HasPrecision(12, 2);
            configuration.HasIndex(item => new { item.OrganizationId, item.IdempotencyKey }).IsUnique();
            configuration.HasQueryFilter(item => item.OrganizationId == organizationContext.OrganizationId);
        });

        modelBuilder.Entity<OrderLifecycleEvent>(configuration =>
        {
            configuration.ToTable("order_lifecycle_events");
            configuration.HasKey(item => item.Id);
            configuration.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            configuration.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
            configuration.Property(item => item.FinancialCreditReversed).HasPrecision(12, 2);
            configuration.HasIndex(item => new { item.OrganizationId, item.IdempotencyKey }).IsUnique();
            configuration.HasIndex(item => new { item.OrganizationId, item.OrderId, item.OccurredAt });
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

            var organizationProperty = entry.Property(nameof(ITenantOwned.OrganizationId));
            if (entry.State == EntityState.Modified
                && organizationProperty.IsModified
                && !Equals(organizationProperty.OriginalValue, organizationProperty.CurrentValue))
            {
                throw new InvalidOperationException("A organização de uma entidade não pode ser alterada.");
            }
        }
    }
}

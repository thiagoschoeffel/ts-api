using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Plans;

public sealed class CommercialPlan : ITenantOwned
{
    private CommercialPlan() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string BenefitDescription { get; private set; } = string.Empty;
    public int DefaultCredits { get; private set; }
    public decimal DefaultPrice { get; private set; }
    public int? ValidityDays { get; private set; }
    public bool IsActive { get; private set; }
    public long Version { get; private set; }

    public static CommercialPlan Create(Guid organizationId, string name, string? description,
        string benefitDescription, int defaultCredits, decimal defaultPrice, int? validityDays) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Version = 1, IsActive = true,
            Name = Required(name, 160), Description = Optional(description, 4_000),
            BenefitDescription = Required(benefitDescription, 500), DefaultCredits = Positive(defaultCredits),
            DefaultPrice = Money(defaultPrice), ValidityDays = Validity(validityDays) };

    public void Update(string name, string? description, string benefitDescription, int defaultCredits,
        decimal defaultPrice, int? validityDays, bool active, long expectedVersion)
    {
        if (Version != expectedVersion) throw new DomainException("O plano foi alterado por outra pessoa.");
        Name = Required(name, 160); Description = Optional(description, 4_000);
        BenefitDescription = Required(benefitDescription, 500); DefaultCredits = Positive(defaultCredits);
        DefaultPrice = Money(defaultPrice); ValidityDays = Validity(validityDays); IsActive = active; Version++;
    }
    private static string Required(string? value, int max) { var v = value?.Trim() ?? ""; if (v.Length is 0 || v.Length > max) throw new DomainException("Os dados do plano são inválidos."); return v; }
    private static string? Optional(string? value, int max) { var v = value?.Trim(); if (v?.Length > max) throw new DomainException("Os dados do plano são inválidos."); return string.IsNullOrEmpty(v) ? null : v; }
    private static int Positive(int value) { if (value <= 0) throw new DomainException("A quantidade de créditos deve ser positiva."); return value; }
    private static decimal Money(decimal value) { if (value < 0) throw new DomainException("O valor do plano não pode ser negativo."); return decimal.Round(value, 2); }
    private static int? Validity(int? value) { if (value <= 0) throw new DomainException("A validade deve ser positiva."); return value; }
}

public sealed class CommercialPlanOffer : ITenantOwned
{
    private CommercialPlanOffer() { }
    public Guid OrganizationId { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid OfferId { get; private set; }
    public static CommercialPlanOffer Create(Guid organizationId, Guid planId, Guid offerId) =>
        organizationId == Guid.Empty || planId == Guid.Empty || offerId == Guid.Empty
            ? throw new DomainException("Plano e oferta compatível são obrigatórios.")
            : new() { OrganizationId = organizationId, PlanId = planId, OfferId = offerId };
}

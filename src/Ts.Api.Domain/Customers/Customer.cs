using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Customers;

public sealed class Customer : ITenantOwned
{
    private Customer() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public string? Notes { get; private set; }
    public string? PreferredDeliveryDriverId { get; private set; }
    public string? PreferredPaymentCondition { get; private set; }
    public string? PreferredPaymentMethod { get; private set; }
    public long Version { get; private set; }

    public static Customer Create(Guid organizationId, string name, string phone) =>
        new() { Id = Guid.NewGuid(), OrganizationId = organizationId, Name = ValidateName(name),
            Phone = ValidatePhone(phone), IsActive = true, Version = 1 };

    public void Update(string name, string phone, bool isActive, string? notes,
        string? preferredDeliveryDriverId, string? preferredPaymentCondition,
        string? preferredPaymentMethod, long expectedVersion)
    {
        if (Version != expectedVersion) throw new DomainException("O cliente foi alterado por outra pessoa.");
        Name = ValidateName(name); Phone = ValidatePhone(phone); IsActive = isActive;
        Notes = Optional(notes, 4_000); PreferredDeliveryDriverId = Optional(preferredDeliveryDriverId, 100);
        PreferredPaymentCondition = Optional(preferredPaymentCondition, 80);
        PreferredPaymentMethod = Optional(preferredPaymentMethod, 80); Version++;
    }

    private static string ValidateName(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 160) throw new DomainException("Informe um nome de cliente válido.");
        return normalized;
    }

    private static string ValidatePhone(string value)
    {
        var normalized = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalized.Length is < 10 or > 15) throw new DomainException("Informe um telefone válido com DDD.");
        return normalized;
    }

    private static string? Optional(string? value, int maximum)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > maximum) throw new DomainException("Um dado opcional excede o tamanho permitido.");
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}

public sealed class CustomerAddress : ITenantOwned
{
    private CustomerAddress() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Street { get; private set; } = string.Empty;
    public string? Number { get; private set; }
    public string? Complement { get; private set; }
    public string? Neighborhood { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? PostalCode { get; private set; }
    public string? ReferencePoint { get; private set; }

    public static CustomerAddress Create(Guid organizationId, Guid customerId, string label, string street,
        string? number, string? complement, string? neighborhood, string? city, string? state,
        string? postalCode, string? referencePoint)
    {
        if (organizationId == Guid.Empty || customerId == Guid.Empty || string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(street))
            throw new DomainException("Rótulo e logradouro são obrigatórios no endereço.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, CustomerId = customerId,
            Label = label.Trim(), Street = street.Trim(), Number = number?.Trim(), Complement = complement?.Trim(),
            Neighborhood = neighborhood?.Trim(), City = city?.Trim(), State = state?.Trim(),
            PostalCode = postalCode?.Trim(), ReferencePoint = referencePoint?.Trim() };
    }
}

public sealed class CustomerPreference : ITenantOwned
{
    private CustomerPreference() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public static CustomerPreference Create(Guid organizationId, Guid customerId, string description)
    {
        var normalized = description?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 500) throw new DomainException("Informe uma preferência válida.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, CustomerId = customerId, Description = normalized };
    }
}

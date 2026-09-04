using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.Production;

public sealed class ProducibleItem
{
    private ProducibleItem() { }

    private ProducibleItem(Guid id, string name)
    {
        Id = id;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static ProducibleItem Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("O nome do item produzível é obrigatório.");
        }

        return new ProducibleItem(Guid.NewGuid(), name.Trim());
    }

    public void Deactivate() => IsActive = false;
}

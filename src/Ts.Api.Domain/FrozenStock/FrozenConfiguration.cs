using Ts.Api.Domain.Common;

namespace Ts.Api.Domain.FrozenStock;

public sealed class FrozenConfiguration
{
    private FrozenConfiguration() { }

    private FrozenConfiguration(
        Guid id,
        Guid offerId,
        Guid producibleItemId,
        string presentation,
        decimal quantityPerUnit,
        MeasurementUnit measurementUnit,
        decimal unitPrice)
    {
        Id = id;
        OfferId = offerId;
        ProducibleItemId = producibleItemId;
        Presentation = presentation;
        QuantityPerUnit = quantityPerUnit;
        MeasurementUnit = measurementUnit;
        UnitPrice = unitPrice;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid OfferId { get; private set; }
    public Guid ProducibleItemId { get; private set; }
    public string Presentation { get; private set; } = string.Empty;
    public decimal QuantityPerUnit { get; private set; }
    public MeasurementUnit MeasurementUnit { get; private set; }
    public decimal UnitPrice { get; private set; }
    public bool IsActive { get; private set; }

    public static FrozenConfiguration Create(
        Guid offerId,
        Guid producibleItemId,
        string presentation,
        decimal quantityPerUnit,
        MeasurementUnit measurementUnit,
        decimal unitPrice)
    {
        if (offerId == Guid.Empty)
        {
            throw new DomainException("A configuração deve referenciar uma oferta existente.");
        }

        if (producibleItemId == Guid.Empty)
        {
            throw new DomainException("A configuração deve referenciar um item produzível existente.");
        }

        if (string.IsNullOrWhiteSpace(presentation))
        {
            throw new DomainException("A apresentação é obrigatória.");
        }

        if (quantityPerUnit <= 0)
        {
            throw new DomainException("A quantidade por unidade deve ser positiva.");
        }

        if (!Enum.IsDefined(measurementUnit))
        {
            throw new DomainException("A unidade de medida é inválida.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("O preço unitário não pode ser negativo.");
        }

        return new FrozenConfiguration(
            Guid.NewGuid(),
            offerId,
            producibleItemId,
            presentation.Trim(),
            quantityPerUnit,
            measurementUnit,
            unitPrice);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

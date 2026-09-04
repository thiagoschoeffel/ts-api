namespace Ts.Api.Domain.FrozenStock;

public static class FrozenShelfLifePolicy
{
    public const int ShelfLifeInDays = 90;

    public static DateOnly CalculateExpiration(DateOnly manufacturedOn) =>
        manufacturedOn.AddDays(ShelfLifeInDays);
}

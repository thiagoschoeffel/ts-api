using Ts.Api.Domain.FrozenStock;

namespace Ts.Api.Domain.Tests.FrozenStock;

public sealed class FrozenShelfLifePolicyTests
{
    [Theory]
    [InlineData(2026, 8, 3, 2026, 11, 1)]
    [InlineData(2026, 11, 26, 2027, 2, 24)]
    [InlineData(2024, 2, 29, 2024, 5, 29)]
    [InlineData(2023, 12, 31, 2024, 3, 30)]
    public void CalculateExpiration_AddsNinetyCalendarDays(
        int year,
        int month,
        int day,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var manufacturedOn = new DateOnly(year, month, day);

        var result = FrozenShelfLifePolicy.CalculateExpiration(manufacturedOn);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), result);
    }
}

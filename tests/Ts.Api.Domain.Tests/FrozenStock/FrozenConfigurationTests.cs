using Ts.Api.Domain.Common;
using Ts.Api.Domain.FrozenStock;

namespace Ts.Api.Domain.Tests.FrozenStock;

public sealed class FrozenConfigurationTests
{
    [Fact]
    public void Create_PreservesVariableUnitPrice()
    {
        var configuration = FrozenConfiguration.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "300 g",
            300,
            MeasurementUnit.Gram,
            24.90m);

        Assert.Equal(24.90m, configuration.UnitPrice);
    }

    [Fact]
    public void Create_RejectsNegativePrice()
    {
        var action = () => FrozenConfiguration.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "300 g",
            300,
            MeasurementUnit.Gram,
            -0.01m);

        Assert.Throws<DomainException>(action);
    }
}

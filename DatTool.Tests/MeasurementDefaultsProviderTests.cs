using DatTool.Domain;
using DatTool.Services;

namespace DatTool.Tests;

public class MeasurementDefaultsProviderTests
{
    private readonly IMeasurementDefaultsProvider _provider = new MeasurementDefaultsProvider();

    [Fact]
    public void GetDefaults_HeatCapacity_UsesPreferredColumns()
    {
        var columns = new[]
        {
            new DatColumn(0, "Sample Temp (Kelvin)", "Sample Temp (Kelvin)", "K", true),
            new DatColumn(1, "Samp HC (J/mole-K)", "Samp HC (J/mole-K)", "J/mole-K", true),
            new DatColumn(2, "Field (Oersted)", "Field (Oersted)", "Oe", true)
        };

        var defaults = _provider.GetDefaults(MeasurementType.HeatCapacity, columns);

        Assert.Equal("Sample Temp (Kelvin)", defaults.XAxisColumn);
        Assert.Equal("Samp HC (J/mole-K)", defaults.YAxisColumn);
        Assert.Equal("Field (Oersted)", defaults.FilterColumn);
        Assert.Equal(50, defaults.DefaultTolerance);
        Assert.Equal(20, defaults.DefaultMinContinuousRows);
    }

    [Fact]
    public void GetDefaults_UnknownMeasurement_FallsBackToNumericOrder()
    {
        var columns = new[]
        {
            new DatColumn(0, "Label", "Label", null, false),
            new DatColumn(1, "ValueA", "ValueA", null, true),
            new DatColumn(2, "ValueB", "ValueB", null, true),
            new DatColumn(3, "ValueC", "ValueC", null, true)
        };

        var defaults = _provider.GetDefaults(MeasurementType.Unknown, columns);

        Assert.Equal("ValueA", defaults.XAxisColumn);
        Assert.Equal("ValueB", defaults.YAxisColumn);
        Assert.Equal("ValueC", defaults.FilterColumn);
        Assert.Equal(1, defaults.DefaultTolerance);
        Assert.Equal(5, defaults.DefaultMinContinuousRows);
    }
}


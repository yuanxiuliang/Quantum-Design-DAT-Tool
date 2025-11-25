using DatTool.Domain;
using DatTool.Services;

namespace DatTool.Tests;

public class DatFileParserTests
{
    private readonly IDatFileParser _parser = new DatFileParser();

    [Theory]
    [InlineData("HeatCapacity.Dat", MeasurementType.HeatCapacity, "Sample Temp (Kelvin)", "Samp HC (J/mole-K)")]
    [InlineData("Resistivity.dat", MeasurementType.Resistivity, "Temperature (K)", "Bridge 1 Resistivity (Ohm)")]
    [InlineData("VSM Data File.dat", MeasurementType.Magnetization, "Magnetic Field (Oe)", "Moment (emu)")]
    public async Task ParseAsync_DetectsMeasurementTypeAndColumns(string fileName, MeasurementType expectedType, string expectedColumnA, string expectedColumnB)
    {
        var path = TestDataHelper.GetDataPath(fileName);
        var result = await _parser.ParseAsync(path);

        Assert.Equal(expectedType, result.MeasurementType);
        Assert.True(result.Columns.Count > 0);
        Assert.True(result.Rows.Count > 0);
        Assert.Contains(result.Columns, c => c.Name.Equals(expectedColumnA, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Columns, c => c.Name.Equals(expectedColumnB, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ReadsMetadataFromFileOpenTime()
    {
        var path = TestDataHelper.GetDataPath("Resistivity.dat");
        var result = await _parser.ParseAsync(path);

        Assert.NotNull(result.Metadata);
        Assert.Equal(1639473183.00, result.Metadata.FileOpenTimestampSeconds);
        Assert.True(result.Metadata.FileOpenDateTime.HasValue);
    }
}


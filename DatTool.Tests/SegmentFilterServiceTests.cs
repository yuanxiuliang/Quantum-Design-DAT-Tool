using DatTool.Domain;
using DatTool.Services;
using System.Globalization;

namespace DatTool.Tests;

public class SegmentFilterServiceTests
{
    private readonly ISegmentFilterService _service = new SegmentFilterService();

    [Fact]
    public void FindSegments_ReturnsContinuousMatches()
    {
        var measurementSet = CreateMeasurementSet(new[]
        {
            10.0, 10.1, 9.9, // matches mean 10 ±0.2
            11.5, 11.4, 11.6,
            9.95, 10.05, 10.1 // second matching block
        });

        var criteria = new FilterCriteria("Field", 10.0, 0.2, 2, true);

        var segments = _service.FindSegments(measurementSet, criteria);

        Assert.Equal(2, segments.Count);
        Assert.Equal((1, 3), (segments[0].StartRow, segments[0].EndRow));
        Assert.Equal((7, 9), (segments[1].StartRow, segments[1].EndRow));
    }

    [Fact]
    public void FindSegments_DisabledCriteria_ReturnsEmpty()
    {
        var measurementSet = CreateMeasurementSet(new[] { 1.0, 2.0, 3.0 });
        var criteria = new FilterCriteria("Field", 2.0, 0.5, 1, false);

        var segments = _service.FindSegments(measurementSet, criteria);

        Assert.Empty(segments);
    }

    [Fact]
    public void FindSegments_NonNumericColumnThrows()
    {
        var column = new DatColumn(0, "Label", "Label", null, false);
        var rows = new[]
        {
            new DatRow(1, new Dictionary<string, DatValue> { ["Label"] = new DatValue("abc", null) })
        };
        var measurementSet = new MeasurementSet(
            "test",
            "test",
            MeasurementType.Unknown,
            new[] { column },
            rows,
            DateTimeOffset.UtcNow,
            new MeasurementMetadata(null, null, null, null));

        var criteria = new FilterCriteria("Label", 0, 1, 1, true);

        Assert.Throws<InvalidOperationException>(() => _service.FindSegments(measurementSet, criteria));
    }

    [Fact]
    public void FindSegments_AutoDetectMean_FindsMultipleBuckets()
    {
        var measurementSet = CreateMeasurementSet(new[]
        {
            5.0, 5.1, 5.05, // first block
            10.0, 10.2,     // short block below min rows
            20.0, 20.1, 20.2, 20.15 // second block
        });

        var criteria = new FilterCriteria("Field", 0, 0.2, 3, true, AutoDetectMean: true);

        var segments = _service.FindSegments(measurementSet, criteria);

        Assert.Equal(2, segments.Count);
        Assert.Contains(segments, s => s.StartRow == 1 && s.EndRow == 3);
        Assert.Contains(segments, s => s.StartRow == 6 && s.EndRow == 9);
    }

    private static MeasurementSet CreateMeasurementSet(double[] values)
    {
        var column = new DatColumn(0, "Field", "Field", "Oe", true);
        var rows = values
            .Select((value, index) => new DatRow(
                index + 1,
                new Dictionary<string, DatValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Field"] = new DatValue(value.ToString(CultureInfo.InvariantCulture), value)
                }))
            .ToArray();

        return new MeasurementSet(
            "test",
            "test",
            MeasurementType.Magnetization,
            new[] { column },
            rows,
            DateTimeOffset.UtcNow,
            new MeasurementMetadata(null, null, null, null));
    }
}


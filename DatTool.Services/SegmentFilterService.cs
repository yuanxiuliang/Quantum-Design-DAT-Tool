using DatTool.Domain;
using System.Linq;

namespace DatTool.Services;

public sealed class SegmentFilterService : ISegmentFilterService
{
    public IReadOnlyList<DataSegment> FindSegments(MeasurementSet measurementSet, FilterCriteria criteria)
    {
        if (measurementSet is null)
        {
            throw new ArgumentNullException(nameof(measurementSet));
        }

        if (criteria is null)
        {
            throw new ArgumentNullException(nameof(criteria));
        }

        if (!criteria.IsEnabled)
        {
            return Array.Empty<DataSegment>();
        }

        var column = measurementSet.Columns.FirstOrDefault(c =>
            c.Name.Equals(criteria.ColumnName, StringComparison.OrdinalIgnoreCase));

        if (column is null)
        {
            throw new ArgumentException($"Column {criteria.ColumnName} does not exist.", nameof(criteria));
        }

        if (!column.IsNumeric)
        {
            throw new InvalidOperationException($"Column {column.Name} is not numeric and cannot be filtered.");
        }

        if (criteria.AutoDetectMean)
        {
            return AutoDetectSegments(measurementSet, column, criteria);
        }

        var tolerance = Math.Max(criteria.Tolerance, 0);
        var minRows = Math.Max(criteria.MinContinuousRows, 1);

        var values = measurementSet.Rows
            .Select(row => row[column.Name]?.Numeric)
            .ToArray();

        var segments = new List<DataSegment>();
        var currentStart = -1;

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            var matches = value.HasValue && Math.Abs(value.Value - criteria.Mean) <= tolerance;

            if (matches && currentStart == -1)
            {
                currentStart = i;
            }

            if ((!matches || i == values.Length - 1) && currentStart != -1)
            {
                var endIndex = matches && i == values.Length - 1 ? i : i - 1;
                var length = endIndex - currentStart + 1;
                if (length >= minRows)
                {
                    segments.Add(CreateSegment(measurementSet, column, currentStart, endIndex));
                }

                currentStart = -1;
            }
        }

        return segments;
    }

    private static DataSegment CreateSegment(MeasurementSet measurementSet, DatColumn column, int startIndex, int endIndex)
    {
        var rows = measurementSet.Rows.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
        var numericValues = rows
            .Select(r => r[column.Name]?.Numeric)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();

        var stats = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mean"] = numericValues.Length > 0 ? numericValues.Average() : null,
            ["StdDev"] = numericValues.Length > 1 ? StdDev(numericValues) : null,
            ["Min"] = numericValues.Length > 0 ? numericValues.Min() : null,
            ["Max"] = numericValues.Length > 0 ? numericValues.Max() : null
        };

        var id = $"segment_{startIndex + 1}_{endIndex + 1}";
        return new DataSegment(
            id,
            startIndex + 1,
            endIndex + 1,
            measurementSet.MeasurementType,
            stats,
            rows);
    }

    private static double StdDev(IReadOnlyList<double> values)
    {
        if (values.Count <= 1)
        {
            return 0;
        }

        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    private IReadOnlyList<DataSegment> AutoDetectSegments(MeasurementSet measurementSet, DatColumn column, FilterCriteria criteria)
    {
        var tolerance = Math.Max(criteria.Tolerance, 0);
        var allowedRange = Math.Max(tolerance * 2, 1e-9);
        var minRows = Math.Max(criteria.MinContinuousRows, 1);
        var values = measurementSet.Rows
            .Select(row => row[column.Name]?.Numeric)
            .ToArray();

        var segments = new List<DataSegment>();
        var index = 0;

        while (index < values.Length)
        {
            var currentValue = values[index];
            if (!currentValue.HasValue)
            {
                index++;
                continue;
            }

            var start = index;
            var end = index;
            var currentMin = currentValue.Value;
            var currentMax = currentValue.Value;

            var cursor = index + 1;
            while (cursor < values.Length)
            {
                var nextValue = values[cursor];
                if (!nextValue.HasValue)
                {
                    break;
                }

                var candidateMin = Math.Min(currentMin, nextValue.Value);
                var candidateMax = Math.Max(currentMax, nextValue.Value);

                if ((candidateMax - candidateMin) <= allowedRange)
                {
                    currentMin = candidateMin;
                    currentMax = candidateMax;
                    end = cursor;
                    cursor++;
                }
                else
                {
                    break;
                }
            }

            var length = end - start + 1;
            if (length >= minRows)
            {
                segments.Add(CreateSegment(measurementSet, column, start, end));
                index = end + 1;
            }
            else
            {
                index = start + 1;
            }
        }

        return segments
            .OrderByDescending(s => s.Rows.Count)
            .ThenBy(s => s.StartRow)
            .ToList();
    }
}


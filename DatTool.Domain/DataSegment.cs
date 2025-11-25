using System.Collections.Immutable;

namespace DatTool.Domain;

/// <summary>
/// Represents a continuous block of data matching a filter criteria selection.
/// </summary>
public sealed class DataSegment
{
    public DataSegment(
        string id,
        int startRow,
        int endRow,
        MeasurementType measurementType,
        IReadOnlyDictionary<string, double?> statistics,
        IReadOnlyList<DatRow> rows)
    {
        if (startRow > endRow)
        {
            throw new ArgumentException("startRow must be <= endRow", nameof(startRow));
        }

        Id = id;
        StartRow = startRow;
        EndRow = endRow;
        MeasurementType = measurementType;
        Statistics = statistics.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        Rows = rows.ToImmutableArray();
    }

    public string Id { get; }

    public int StartRow { get; }

    public int EndRow { get; }

    public MeasurementType MeasurementType { get; }

    public IReadOnlyDictionary<string, double?> Statistics { get; }

    public IReadOnlyList<DatRow> Rows { get; }
}


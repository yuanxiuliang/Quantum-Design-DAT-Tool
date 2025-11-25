using System.Collections.Immutable;

namespace DatTool.Domain;

/// <summary>
/// Container for a parsed DAT file, including metadata and raw rows.
/// </summary>
public sealed class MeasurementSet
{
    public MeasurementSet(
        string filePath,
        string displayName,
        MeasurementType measurementType,
        IEnumerable<DatColumn> columns,
        IEnumerable<DatRow> rows,
        DateTimeOffset loadedAt,
        MeasurementMetadata metadata)
    {
        FilePath = filePath;
        DisplayName = displayName;
        MeasurementType = measurementType;
        LoadedAt = loadedAt;
        Columns = columns.ToImmutableArray();
        Rows = rows.ToImmutableArray();
        Metadata = metadata;
    }

    public string FilePath { get; }

    public string DisplayName { get; }

    public MeasurementType MeasurementType { get; private set; }

    public DateTimeOffset LoadedAt { get; }

    public IReadOnlyList<DatColumn> Columns { get; }

    public IReadOnlyList<DatRow> Rows { get; }

    public MeasurementMetadata Metadata { get; }

    public void UpdateMeasurementType(MeasurementType measurementType)
    {
        MeasurementType = measurementType;
    }
}


using System.Collections.Immutable;

namespace DatTool.Domain;

/// <summary>
/// Represents a single row of data parsed from a DAT file.
/// </summary>
public sealed class DatRow
{
    public DatRow(int index, IReadOnlyDictionary<string, DatValue> values)
    {
        Index = index;
        Values = values.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public int Index { get; }

    public IReadOnlyDictionary<string, DatValue> Values { get; }

    public DatValue? this[string columnName] =>
        Values.TryGetValue(columnName, out var value) ? value : null;
}


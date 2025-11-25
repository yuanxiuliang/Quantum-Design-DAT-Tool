namespace DatTool.Domain;

/// <summary>
/// Represents a column defined in a DAT file.
/// </summary>
public sealed record DatColumn(
    int Index,
    string Name,
    string DisplayName,
    string? Unit,
    bool IsNumeric);


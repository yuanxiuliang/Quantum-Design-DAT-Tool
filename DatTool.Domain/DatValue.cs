namespace DatTool.Domain;

/// <summary>
/// Represents the parsed value of a single cell in the DAT table.
/// </summary>
public sealed record DatValue(string? Raw, double? Numeric);


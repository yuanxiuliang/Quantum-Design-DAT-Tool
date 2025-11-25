namespace DatTool.Domain;

/// <summary>
/// Additional context extracted from the DAT header.
/// </summary>
public sealed record MeasurementMetadata(
    string? InstrumentApp,
    string? InstrumentVersion,
    double? FileOpenTimestampSeconds,
    DateTimeOffset? FileOpenDateTime);


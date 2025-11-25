namespace DatTool.Domain;

/// <summary>
/// Captures the default plotting & filtering configuration for a measurement type.
/// </summary>
public sealed record MeasurementDefaults(
    MeasurementType MeasurementType,
    string XAxisColumn,
    string YAxisColumn,
    string FilterColumn,
    double DefaultTolerance,
    int DefaultMinContinuousRows);


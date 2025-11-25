namespace DatTool.Domain;

/// <summary>
/// Describes the parameters a user can adjust when extracting data segments.
/// </summary>
public sealed record FilterCriteria(
    string ColumnName,
    double Mean,
    double Tolerance,
    int MinContinuousRows,
    bool IsEnabled,
    bool AutoDetectMean = false);


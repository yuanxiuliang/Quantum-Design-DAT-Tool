namespace DatTool.Services;

public sealed record DatFileParserOptions(
    IReadOnlyList<string> PreferredEncodings,
    bool InferMeasurementType)
{
    public static DatFileParserOptions Default { get; } = new(
        new[] { "utf-8", "utf-16", "utf-16le", "utf-16be", "iso-8859-1" },
        true);
}


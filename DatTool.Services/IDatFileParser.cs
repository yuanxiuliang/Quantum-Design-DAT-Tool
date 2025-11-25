using DatTool.Domain;

namespace DatTool.Services;

public interface IDatFileParser
{
    Task<MeasurementSet> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}


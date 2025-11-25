using DatTool.Domain;

namespace DatTool.Services;

public interface IMeasurementDefaultsProvider
{
    MeasurementDefaults GetDefaults(MeasurementType measurementType, IReadOnlyList<DatColumn> availableColumns);
}


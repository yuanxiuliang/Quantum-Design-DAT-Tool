using DatTool.Domain;

namespace DatTool.Services;

public interface ISegmentFilterService
{
    IReadOnlyList<DataSegment> FindSegments(MeasurementSet measurementSet, FilterCriteria criteria);
}


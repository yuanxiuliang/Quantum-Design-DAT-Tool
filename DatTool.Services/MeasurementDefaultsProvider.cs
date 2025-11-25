using DatTool.Domain;
using System.Linq;

namespace DatTool.Services;

public sealed class MeasurementDefaultsProvider : IMeasurementDefaultsProvider
{
    private static readonly IReadOnlyDictionary<MeasurementType, DefaultColumnCandidates> Candidates =
        new Dictionary<MeasurementType, DefaultColumnCandidates>
        {
            {
                MeasurementType.HeatCapacity,
                new DefaultColumnCandidates(
                    XAxis: new[]
                    {
                        "Sample Temp (Kelvin)",
                        "Puck Temp (Kelvin)",
                        "System Temp (Kelvin)",
                        "Temperature (K)"
                    },
                    YAxis: new[]
                    {
                        "Samp HC (J/mole-K)",
                        "Samp HC (mJ/g-K)",
                        "Samp HC/Temp (J/mole-K/K)",
                        "Total HC (µJ/K)",
                        "Total HC (�J/K)"
                    },
                    Filter: new[]
                    {
                        "Magnetic Field (Oe)",
                        "Field (Oersted)",
                        "Field (Oe)"
                    },
                    DefaultTolerance: 50,
                    DefaultMinRows: 20)
            },
            {
                MeasurementType.Magnetization,
                new DefaultColumnCandidates(
                    XAxis: new[]
                    {
                        "Magnetic Field (Oe)",
                        "Field (Oe)",
                        "Time Stamp (sec)"
                    },
                    YAxis: new[]
                    {
                        "Moment (emu)",
                        "M. Raw' (emu)"
                    },
                    Filter: new[]
                    {
                        "Sample Temp (Kelvin)",
                        "Temperature (K)",
                        "System Temp. (K)"
                    },
                    DefaultTolerance: 0.2,
                    DefaultMinRows: 10)
            },
            {
                MeasurementType.Resistivity,
                new DefaultColumnCandidates(
                    XAxis: new[]
                    {
                        "Temperature (K)",
                        "Sample Temp (Kelvin)",
                        "Magnetic Field (Oe)"
                    },
                    YAxis: new[]
                    {
                        "Bridge 1 Resistivity (Ohm)",
                        "Bridge 2 Resistivity (Ohm)",
                        "Bridge 1 Resistance (Ohms)",
                        "Resistance (Ohms)"
                    },
                    Filter: new[]
                    {
                        "Magnetic Field (Oe)",
                        "Field (Oersted)"
                    },
                    DefaultTolerance: 50,
                    DefaultMinRows: 15)
            }
        };

    public MeasurementDefaults GetDefaults(MeasurementType measurementType, IReadOnlyList<DatColumn> availableColumns)
    {
        if (availableColumns is null || availableColumns.Count == 0)
        {
        throw new ArgumentException("Column metadata is required to determine defaults.", nameof(availableColumns));
        }

        if (Candidates.TryGetValue(measurementType, out var candidates))
        {
            var x = PickColumn(availableColumns, candidates.XAxis);
            var y = PickColumn(availableColumns, candidates.YAxis);
            var filter = PickColumn(availableColumns, candidates.Filter);

            if (x is not null && y is not null && filter is not null)
            {
                return new MeasurementDefaults(
                    measurementType,
                    x,
                    y,
                    filter,
                    candidates.DefaultTolerance,
                    candidates.DefaultMinRows);
            }
        }

        return BuildFallbackDefaults(measurementType, availableColumns);
    }

    private static string? PickColumn(IReadOnlyList<DatColumn> availableColumns, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var match = availableColumns.FirstOrDefault(c =>
                c.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.Name;
            }
        }

        return null;
    }

    private static MeasurementDefaults BuildFallbackDefaults(MeasurementType measurementType, IReadOnlyList<DatColumn> availableColumns)
    {
        var numericColumns = availableColumns.Where(c => c.IsNumeric).ToList();
        var x = numericColumns.FirstOrDefault()?.Name ?? availableColumns[0].Name;
        var y = numericColumns.Skip(1).FirstOrDefault()?.Name ?? x;
        var filter = numericColumns.FirstOrDefault(c =>
                !c.Name.Equals(x, StringComparison.OrdinalIgnoreCase) &&
                !c.Name.Equals(y, StringComparison.OrdinalIgnoreCase))
            ?.Name
            ?? availableColumns.FirstOrDefault(c =>
                    !c.Name.Equals(x, StringComparison.OrdinalIgnoreCase) &&
                    !c.Name.Equals(y, StringComparison.OrdinalIgnoreCase))
                ?.Name
            ?? x;

        return new MeasurementDefaults(
            measurementType,
            x,
            y,
            filter,
            1,
            5);
    }

    private sealed record DefaultColumnCandidates(
        IReadOnlyList<string> XAxis,
        IReadOnlyList<string> YAxis,
        IReadOnlyList<string> Filter,
        double DefaultTolerance,
        int DefaultMinRows);
}


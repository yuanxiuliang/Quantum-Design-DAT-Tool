using System.Globalization;
using System.Text;
using DatTool.Domain;

namespace DatTool.Services;

public sealed class DatFileParser : IDatFileParser
{
    private readonly DatFileParserOptions _options;

    public DatFileParser(DatFileParserOptions? options = null)
    {
        _options = options ?? DatFileParserOptions.Default;
    }

    public async Task<MeasurementSet> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
        throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
        throw new FileNotFoundException("DAT file not found.", filePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var header = await ReadHeaderAsync(filePath, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = ParseMetadata(header.HeaderLines);
        var measurementType = _options.InferMeasurementType
            ? DetectMeasurementType(header.HeaderLines)
            : MeasurementType.Unknown;

        var (columns, rows) = await ReadDataAsync(filePath, header, cancellationToken).ConfigureAwait(false);

        var measurementSet = new MeasurementSet(
            filePath,
            Path.GetFileName(filePath),
            measurementType,
            columns,
            rows,
            DateTimeOffset.UtcNow,
            metadata);

        return measurementSet;
    }

    private async Task<HeaderReadResult> ReadHeaderAsync(string filePath, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        foreach (var encodingName in _options.PreferredEncodings)
        {
            Encoding encoding;
            try
            {
                encoding = Encoding.GetEncoding(encodingName);
            }
            catch (Exception ex)
            {
                lastError = ex;
                continue;
            }

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
                var lines = new List<string>();
                string? line;
                var lineIndex = 0;

                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    lines.Add(line);
                    if (line.Trim().Equals("[Data]", StringComparison.OrdinalIgnoreCase))
                    {
                        return new HeaderReadResult(lines, lineIndex + 1, reader.CurrentEncoding);
                    }

                    lineIndex++;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidDataException("Failed to read DAT header or locate the [Data] marker.", lastError);
    }

    private static MeasurementMetadata ParseMetadata(IReadOnlyList<string> headerLines)
    {
        string? app = null;
        string? version = null;
        double? ts = null;
        DateTimeOffset? dt = null;

        foreach (var rawLine in headerLines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("BYAPP", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    app = parts[1];
                }
                if (parts.Length >= 3)
                {
                    version = parts[2];
                }
            }
            else if (line.StartsWith("FILEOPENTIME", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var timestamp))
                {
                    ts = timestamp;
                }

                if (parts.Length >= 4)
                {
                    var datePart = parts[2];
                    var timePart = parts[3];
                    var timestampString = $"{datePart} {timePart}";

                    var formats = new[]
                    {
                        "MM/dd/yyyy h:mm tt",
                        "MM/dd/yyyy hh:mm tt",
                        "yyyy-MM-dd HH:mm:ss",
                        "yyyy/MM/dd HH:mm:ss",
                        "dd/MM/yyyy HH:mm:ss",
                        "MM/dd/yyyy HH:mm:ss"
                    };

                    if (DateTimeOffset.TryParseExact(
                            timestampString,
                            formats,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeLocal,
                            out var parsed))
                    {
                        dt = parsed;
                    }
                    else if (DateTimeOffset.TryParse(timestampString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
                    {
                        dt = parsed;
                    }
                }
            }
        }

        return new MeasurementMetadata(app, version, ts, dt);
    }

    private static MeasurementType DetectMeasurementType(IReadOnlyList<string> headerLines)
    {
        foreach (var rawLine in headerLines)
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("BYAPP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var label = parts[1].ToLowerInvariant();
            if (label.Contains("heat"))
            {
                return MeasurementType.HeatCapacity;
            }
            if (label.Contains("vsm") || label.Contains("magnet"))
            {
                return MeasurementType.Magnetization;
            }
            if (label.Contains("resist") || label.Contains("transport"))
            {
                return MeasurementType.Resistivity;
            }
        }

        return MeasurementType.Unknown;
    }

    private async Task<(IReadOnlyList<DatColumn> Columns, IReadOnlyList<DatRow> Rows)> ReadDataAsync(
        string filePath,
        HeaderReadResult header,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        using var reader = new StreamReader(stream, header.Encoding, detectEncodingFromByteOrderMarks: true);

        var currentLine = 0;
        while (currentLine < header.DataHeaderLineIndex && await reader.ReadLineAsync().ConfigureAwait(false) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentLine++;
        }

        var columnLine = await reader.ReadLineAsync().ConfigureAwait(false)
                         ?? throw new InvalidDataException("DAT file is missing column definitions.");

        var columnNamesRaw = ParseCsvLine(columnLine);
        var columnNames = NormalizeColumnNames(columnNamesRaw);
        var columns = columnNames
            .Select((name, index) => new DatColumn(index, name, name, ExtractUnit(name), true))
            .ToArray();

        var numericFlags = new bool[columns.Length];
        Array.Fill(numericFlags, true);

        var rows = new List<DatRow>();
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = ParseCsvLine(line);
            if (cells.Count == 0)
            {
                continue;
            }

            var valueMap = new Dictionary<string, DatValue>(columns.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Length; i++)
            {
                var raw = i < cells.Count ? cells[i]?.Trim() : null;
                var numeric = TryParseDouble(raw, out var number);

                if (!numeric && !string.IsNullOrEmpty(raw))
                {
                    numericFlags[i] = false;
                }

                valueMap[columns[i].Name] = new DatValue(raw, numeric ? number : null);
            }

            rows.Add(new DatRow(rows.Count + 1, valueMap));
        }

        var finalizedColumns = columns
            .Select((col, index) => col with { IsNumeric = numericFlags[index] })
            .ToArray();

        return (finalizedColumns, rows);
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        if (line == null)
        {
            return values;
        }

        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        values.Add(sb.ToString());
        return values;
    }

    private static IReadOnlyList<string> NormalizeColumnNames(IReadOnlyList<string> columns)
    {
        var normalized = new List<string>(columns.Count);
        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < columns.Count; i++)
        {
            var raw = columns[i]?.Trim() ?? string.Empty;
            if (raw.StartsWith("\"") && raw.EndsWith("\""))
            {
                raw = raw.Trim('"');
            }

            var name = string.IsNullOrWhiteSpace(raw) ? $"Unnamed_{i}" : raw;
            if (nameCounts.TryGetValue(name, out var count))
            {
                count++;
                nameCounts[name] = count;
                name = $"{name}_{count}";
            }
            else
            {
                nameCounts[name] = 0;
            }

            normalized.Add(name);
        }

        return normalized;
    }

    private static bool TryParseDouble(string? raw, out double value)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = default;
            return false;
        }

        var style = NumberStyles.Float | NumberStyles.AllowThousands;
        if (double.TryParse(raw, style, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return double.TryParse(raw, style, CultureInfo.CurrentCulture, out value);
    }

    private static string? ExtractUnit(string columnName)
    {
        var start = columnName.IndexOf("(", StringComparison.Ordinal);
        var end = columnName.IndexOf(")", start + 1, StringComparison.Ordinal);
        if (start >= 0 && end > start)
        {
            return columnName[(start + 1)..end].Trim();
        }

        return null;
    }

    private sealed record HeaderReadResult(
        IReadOnlyList<string> HeaderLines,
        int DataHeaderLineIndex,
        Encoding Encoding);
}


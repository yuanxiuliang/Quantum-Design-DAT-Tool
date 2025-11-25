using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatTool.Domain;
using DatTool.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Text;

namespace DatTool.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public event Action? PlotResetRequested;
    private readonly IDatFileParser _parser;
    private readonly IMeasurementDefaultsProvider _defaultsProvider;
    private readonly ISegmentFilterService _segmentFilterService;
    private MeasurementSet? _currentMeasurement;
    private List<DataSegment> _multiSelectedSegments = new();
    private readonly Dictionary<string, (MarkerType marker, bool useLine)> _plotModeMap = new()
    {
        { "Line", (MarkerType.None, true) },
        { "Scatter", (MarkerType.Circle, false) },
        { "Line + Scatter", (MarkerType.Circle, true) }
    };

    public MainViewModel(
        IDatFileParser parser,
        IMeasurementDefaultsProvider defaultsProvider,
        ISegmentFilterService segmentFilterService)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _defaultsProvider = defaultsProvider ?? throw new ArgumentNullException(nameof(defaultsProvider));
        _segmentFilterService = segmentFilterService ?? throw new ArgumentNullException(nameof(segmentFilterService));

        LoadFileCommand = new AsyncRelayCommand<string?>(LoadFileAsync);
        ApplyFilterCommand = new RelayCommand(ApplyFilter, CanApplyFilter);

        PlotModel = new PlotModel
        {
            DefaultFont = "Arial",
            DefaultFontSize = 18
        };
    }

    public ObservableCollection<string> AvailableColumns { get; } = new();

    public ObservableCollection<DataSegment> FilteredSegments { get; } = new();

    public IAsyncRelayCommand<string?> LoadFileCommand { get; }

    public IRelayCommand ApplyFilterCommand { get; }

    public PlotModel PlotModel { get; }

    public IReadOnlyList<string> PlotModes { get; } = new[] { "Line", "Scatter", "Line + Scatter" };

    [ObservableProperty]
    private string selectedPlotMode = "Line";

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private string? currentFilePath;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private MeasurementType measurementType;

    [ObservableProperty]
    private string? selectedXAxisColumn;

    [ObservableProperty]
    private string? selectedYAxisColumn;

    [ObservableProperty]
    private string? filterColumn;

    [ObservableProperty]
    private string? filterMeanInput = string.Empty;

[ObservableProperty]
private string? filterToleranceInput = string.Empty;

    [ObservableProperty]
    private int minContinuousRows = 5;

    [ObservableProperty]
    private string? lastError;

    [ObservableProperty]
    private DataSegment? selectedSegment;

    partial void OnFilterColumnChanged(string? value) => ApplyFilterCommand.NotifyCanExecuteChanged();

    partial void OnSelectedSegmentChanged(DataSegment? value)
    {
        RenderCurrentView();
        if (value is not null)
        {
            StatusMessage = $"Segment {value.Id} selected ({value.Rows.Count} rows).";
        }
        else
        {
            StatusMessage = "Showing all data.";
        }
    }

    partial void OnSelectedXAxisColumnChanged(string? value) => RenderCurrentView();

    partial void OnSelectedYAxisColumnChanged(string? value) => RenderCurrentView();

    private async Task LoadFileAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Please choose a DAT file to load.";
            LastError = "No file path provided.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading DAT file...";
            LastError = null;

            var measurement = await _parser.ParseAsync(path);
            _currentMeasurement = measurement;
            CurrentFilePath = path;
            MeasurementType = measurement.MeasurementType;

            UpdateColumns(measurement);
            ApplyDefaults(measurement);
            FilteredSegments.Clear();
            SelectedSegment = null;

            StatusMessage = $"Loaded {Path.GetFileName(path)} successfully.";
            RenderCurrentView();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            StatusMessage = "Failed to load file.";
        }
        finally
        {
            IsBusy = false;
            ApplyFilterCommand.NotifyCanExecuteChanged();
        }
    }

    private void ApplyDefaults(MeasurementSet measurement)
    {
        try
        {
            var defaults = _defaultsProvider.GetDefaults(measurement.MeasurementType, measurement.Columns);
            SelectedXAxisColumn = defaults.XAxisColumn;
            SelectedYAxisColumn = defaults.YAxisColumn;
            FilterColumn = defaults.FilterColumn;
            FilterToleranceInput = defaults.DefaultTolerance.ToString("G4", CultureInfo.InvariantCulture);
            MinContinuousRows = defaults.DefaultMinContinuousRows;
            FilterMeanInput = string.Empty;
            RenderCurrentView();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            StatusMessage = "Failed to apply default settings.";
        }
    }

    private void UpdateColumns(MeasurementSet measurement)
    {
        AvailableColumns.Clear();
        foreach (var column in measurement.Columns)
        {
            AvailableColumns.Add(column.Name);
        }
    }

    private void ApplyFilter()
    {
        if (_currentMeasurement is null || string.IsNullOrWhiteSpace(FilterColumn))
        {
            StatusMessage = "No data loaded or filter column is empty.";
            return;
        }

        try
        {
            if (!TryGetFilterMean(out var mean, out var autoDetect))
            {
                LastError = $"Invalid target mean: {FilterMeanInput}";
                StatusMessage = "Enter a valid target mean or leave it blank for auto-detect.";
                return;
            }

            if (!TryGetFilterTolerance(out var tolerance))
            {
                LastError = $"Invalid tolerance: {FilterToleranceInput}";
                StatusMessage = "Enter a valid tolerance value.";
                return;
            }

            var criteria = new FilterCriteria(
                FilterColumn,
                mean,
                Math.Max(0, tolerance),
                Math.Max(1, MinContinuousRows),
                true,
                autoDetect);

            var segments = _segmentFilterService.FindSegments(_currentMeasurement, criteria);
            ReplaceSegments(segments);
            SelectedSegment = segments.FirstOrDefault();
            StatusMessage = segments.Count > 0
                ? $"Filter applied. Found {segments.Count} segments."
                : "No segments matched the criteria. Adjust the parameters.";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            StatusMessage = "Failed to apply filter.";
        }
    }

    private bool CanApplyFilter() =>
        _currentMeasurement is not null &&
        !string.IsNullOrWhiteSpace(FilterColumn);

    private void ReplaceSegments(IEnumerable<DataSegment> segments)
    {
        FilteredSegments.Clear();
        foreach (var segment in segments)
        {
            FilteredSegments.Add(segment);
        }
        _multiSelectedSegments.Clear();
    }

    public void UpdateSelectedSegments(IEnumerable<DataSegment> segments)
    {
        _multiSelectedSegments = segments?.Where(s => s is not null).ToList() ?? new List<DataSegment>();
        if (_multiSelectedSegments.Count > 0)
        {
            StatusMessage = $"Selected {_multiSelectedSegments.Count} segments.";
        }
        RenderCurrentView();
    }

    public async Task ExportSegmentsToCsvAsync(IEnumerable<DataSegment> segments, string filePath)
    {
        if (segments is null)
        {
            throw new ArgumentException("No segments selected.", nameof(segments));
        }

        var segmentList = segments.Where(s => s is not null).ToList();
        if (segmentList.Count == 0)
        {
            throw new InvalidOperationException("Select at least one data segment.");
        }

        var exportColumns = new List<string>();
        if (!string.IsNullOrWhiteSpace(SelectedXAxisColumn))
        {
            exportColumns.Add(SelectedXAxisColumn!);
        }

        if (!string.IsNullOrWhiteSpace(SelectedYAxisColumn))
        {
            exportColumns.Add(SelectedYAxisColumn!);
        }

        if (!string.IsNullOrWhiteSpace(FilterColumn))
        {
            exportColumns.Add(FilterColumn!);
        }

        if (exportColumns.Count == 0)
        {
            throw new InvalidOperationException("Select the columns to export first.");
        }

        var header = new List<string>();
        foreach (var segment in segmentList)
        {
            foreach (var column in exportColumns)
            {
                header.Add($"{column}({segment.StartRow}-{segment.EndRow})");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header.Select(EscapeCsv)));

        var maxRows = segmentList.Max(s => s.Rows.Count);
        for (var rowIndex = 0; rowIndex < maxRows; rowIndex++)
        {
            var rowValues = new List<string>();
            foreach (var segment in segmentList)
            {
                foreach (var column in exportColumns)
                {
                    string cell = string.Empty;
                    if (rowIndex < segment.Rows.Count)
                    {
                        cell = FormatCsvValue(segment.Rows[rowIndex][column]);
                    }

                    rowValues.Add(EscapeCsv(cell));
                }
            }

            sb.AppendLine(string.Join(",", rowValues));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        StatusMessage = $"Exported {segmentList.Count} segments to {Path.GetFileName(filePath)}.";
    }

    private static double? ComputeColumnMean(MeasurementSet measurement, string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return null;
        }

        var values = measurement.Rows
            .Select(row => row[columnName]?.Numeric)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();

        return values.Length == 0 ? null : values.Average();
    }

    private bool TryGetFilterMean(out double mean, out bool autoDetect)
    {
        if (string.IsNullOrWhiteSpace(FilterMeanInput))
        {
            mean = 0;
            autoDetect = true;
            return true;
        }

        var styles = NumberStyles.Float | NumberStyles.AllowThousands;
        if (double.TryParse(FilterMeanInput, styles, CultureInfo.CurrentCulture, out mean) ||
            double.TryParse(FilterMeanInput, styles, CultureInfo.InvariantCulture, out mean))
        {
            autoDetect = false;
            return true;
        }

        autoDetect = false;
        return false;
    }

    private bool TryGetFilterTolerance(out double tolerance)
    {
        if (string.IsNullOrWhiteSpace(FilterToleranceInput))
        {
            tolerance = 0;
            return true;
        }

        var styles = NumberStyles.Float | NumberStyles.AllowThousands;
        if (double.TryParse(FilterToleranceInput, styles, CultureInfo.CurrentCulture, out tolerance) ||
            double.TryParse(FilterToleranceInput, styles, CultureInfo.InvariantCulture, out tolerance))
        {
            return true;
        }

        tolerance = 0;
        return false;
    }

    private void RenderCurrentView()
    {
        if (_currentMeasurement is null ||
            string.IsNullOrWhiteSpace(SelectedXAxisColumn) ||
            string.IsNullOrWhiteSpace(SelectedYAxisColumn))
        {
            PlotModel.Series.Clear();
            PlotModel.Axes.Clear();
            PlotModel.InvalidatePlot(true);
            StatusMessage = "Missing required columns. Cannot render plot.";
            return;
        }

        var segmentsToPlot = _multiSelectedSegments.Count > 0
            ? new List<DataSegment>(_multiSelectedSegments)
            : (SelectedSegment is not null ? new List<DataSegment> { SelectedSegment } : new List<DataSegment>());

        PlotModel.Series.Clear();
        PlotModel.Axes.Clear();
        PlotModel.Axes.Add(CreateAxis(AxisPosition.Bottom, SelectedXAxisColumn));
        PlotModel.Axes.Add(CreateAxis(AxisPosition.Left, SelectedYAxisColumn));

        var plotMode = _plotModeMap.TryGetValue(SelectedPlotMode, out var mode)
            ? mode
            : _plotModeMap["Line"];

        if (segmentsToPlot.Count == 0)
        {
            PlotModel.Series.Add(BuildSeries(_currentMeasurement.Rows, SelectedXAxisColumn!, SelectedYAxisColumn!, "All Data", plotMode));
        }
        else
        {
            foreach (var segment in segmentsToPlot)
            {
                var title = $"{segment.StartRow}-{segment.EndRow}";
                PlotModel.Series.Add(BuildSeries(segment.Rows, SelectedXAxisColumn!, SelectedYAxisColumn!, title, plotMode));
            }
        }

        PlotModel.InvalidatePlot(true);
        PlotResetRequested?.Invoke();
    }

    private static LinearAxis CreateAxis(AxisPosition position, string title) =>
        new()
        {
            Position = position,
            Title = title,
            Font = "Arial",
            TitleFont = "Arial",
            FontSize = 20,
            TitleFontSize = 20
        };

    private static LineSeries BuildSeries(IEnumerable<DatRow> rows, string xColumn, string yColumn, string title, (MarkerType marker, bool useLine) mode)
    {
        var series = new LineSeries
        {
            Title = title,
            StrokeThickness = mode.useLine ? 1.2 : 0,
            MarkerType = mode.marker,
            MarkerSize = mode.marker == MarkerType.None ? 0 : 3
        };

        foreach (var row in rows)
        {
            var x = row[xColumn]?.Numeric;
            var y = row[yColumn]?.Numeric;
            if (x.HasValue && y.HasValue)
            {
                series.Points.Add(new DataPoint(x.Value, y.Value));
            }
        }

        return series;
    }

    partial void OnSelectedPlotModeChanged(string value)
    {
        if (_currentMeasurement is null ||
            string.IsNullOrWhiteSpace(SelectedXAxisColumn) ||
            string.IsNullOrWhiteSpace(SelectedYAxisColumn))
        {
            return;
        }

        RenderCurrentView();
    }

    private static string FormatCsvValue(DatValue? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(value.Raw))
        {
            return value.Raw!;
        }

        if (value.Numeric.HasValue)
        {
            return value.Numeric.Value.ToString("G6", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value.Replace("\"", "\"\"");
        return sanitized.Contains(',') || sanitized.Contains('"')
            ? $"\"{sanitized}\""
            : sanitized;
    }
}


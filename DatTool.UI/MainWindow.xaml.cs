using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DatTool.Domain;
using DatTool.ViewModels;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;

namespace DatTool.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Stack<AxisLimits> _zoomHistory = new();
    private PlotController? _plotController;
    private ListCollectionView? _segmentsView;
    private string _currentSortKey = "StartRow";
    private ListSortDirection _currentSortDirection = ListSortDirection.Ascending;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PlotResetRequested += OnPlotResetRequested;
        ConfigurePlotInteractions();
        InitializeSegmentSorting();
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DAT files (*.dat)|*.dat|All files (*.*)|*.*",
            Title = "Select DAT File"
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadDatFileAsync(dialog.FileName);
        }
    }

    public async Task LoadDatFileAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {       
            MessageBox.Show("No DAT file path was provided.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!File.Exists(filePath))
        {
            MessageBox.Show($"File not found:\n{filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _viewModel.CurrentFilePath = filePath;
        ShowLoadingDialog("Loading file...");
        try
        {
            if (_viewModel.LoadFileCommand.CanExecute(filePath))
            {
                await _viewModel.LoadFileCommand.ExecuteAsync(filePath);
            }
            else
            {
                MessageBox.Show("The DAT file could not be loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load DAT file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            HideLoadingDialog();
        }
    }

    private async void ExportSegmentsButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedSegments = SegmentsList.SelectedItems.Cast<DataSegment>().ToList();
        if (selectedSegments.Count == 0)
        {
            MessageBox.Show("Select at least one data segment.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = "Segments.csv",
            Title = "Export Filtered Segments"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _viewModel.ExportSegmentsToCsvAsync(selectedSegments, dialog.FileName);
                MessageBox.Show("Export completed.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SegmentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = SegmentsList.SelectedItems.Cast<DataSegment>().ToList();
        _viewModel.UpdateSelectedSegments(selected);
    }

    private void SegmentsHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader header || header.Tag is not string sortKey)
        {
            return;
        }

        var nextDirection = (_currentSortKey == sortKey && _currentSortDirection == ListSortDirection.Ascending)
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        ApplySegmentSort(sortKey, nextDirection);
    }

    private void ConfigurePlotInteractions()
    {
        if (MainPlotView is null)
        {
            return;
        }

        _plotController = new PlotController();
        _plotController.UnbindMouseDown(OxyMouseButton.Left);
        _plotController.UnbindMouseDown(OxyMouseButton.Right);

        _plotController.BindMouseDown(OxyMouseButton.Left,
            new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) =>
            {
                CaptureCurrentAxisLimits();
                OxyPlot.PlotCommands.ZoomRectangle.Execute(view, controller, args);
            }));

        _plotController.BindMouseDown(OxyMouseButton.Right,
            new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) =>
            {
                if (TryRestoreAxisLimits())
                {
                    args.Handled = true;
                    MainPlotView.InvalidatePlot(false);
                    return;
                }

                // Already at base zoom level, suppress further right-click handling.
                args.Handled = true;
            }));

        MainPlotView.Controller = _plotController;
    }

    private void InitializeSegmentSorting()
    {
        _segmentsView = CollectionViewSource.GetDefaultView(_viewModel.FilteredSegments) as ListCollectionView;
        if (_segmentsView is null)
        {
            return;
        }

        ApplySegmentSort("StartRow", ListSortDirection.Ascending);
        _viewModel.FilteredSegments.CollectionChanged += OnSegmentsCollectionChanged;
    }

    private async void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(MainViewModel.IsBusy), StringComparison.Ordinal))
        {
            return;
        }

        if (_viewModel.IsBusy)
        {
            ShowLoadingDialog("Parsing DAT file...");
        }
        else
        {
            await Dispatcher.InvokeAsync(HideLoadingDialog);
        }
    }

    private void OnSegmentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _segmentsView?.Refresh();
    }

    private void ApplySegmentSort(string sortKey, ListSortDirection direction)
    {
        if (_segmentsView is null)
        {
            return;
        }

        _currentSortKey = sortKey;
        _currentSortDirection = direction;
        _segmentsView.CustomSort = new DataSegmentComparer(sortKey, direction == ListSortDirection.Ascending);
        _segmentsView.Refresh();
    }

    private void OnPlotResetRequested()
    {
        _zoomHistory.Clear();
    }

    private void CaptureCurrentAxisLimits()
    {
        if (MainPlotView?.Model is null)
        {
            return;
        }

        var axes = GetPrimaryAxes();
        if (axes is null)
        {
            return;
        }

        var (xAxis, yAxis) = axes.Value;
        _zoomHistory.Push(new AxisLimits(xAxis.ActualMinimum, xAxis.ActualMaximum, yAxis.ActualMinimum, yAxis.ActualMaximum));
    }

    private bool TryRestoreAxisLimits()
    {
        if (MainPlotView?.Model is null)
        {
            _zoomHistory.Clear();
            return false;
        }

        if (_zoomHistory.Count == 0)
        {
            return false;
        }

        var axes = GetPrimaryAxes();
        if (axes is null)
        {
            _zoomHistory.Clear();
            return false;
        }

        var limits = _zoomHistory.Pop();
        var (xAxis, yAxis) = axes.Value;

        xAxis.Zoom(limits.XMin, limits.XMax);
        yAxis.Zoom(limits.YMin, limits.YMax);

        xAxis.PlotModel?.InvalidatePlot(false);
        return true;
    }

    private (Axis xAxis, Axis yAxis)? GetPrimaryAxes()
    {
        if (MainPlotView?.Model is null)
        {
            return null;
        }

        var xAxis = MainPlotView.Model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom)
                    ?? MainPlotView.Model.Axes.FirstOrDefault(a => a.IsHorizontal());
        var yAxis = MainPlotView.Model.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left)
                    ?? MainPlotView.Model.Axes.FirstOrDefault(a => a.IsVertical());

        if (xAxis is null || yAxis is null)
        {
            return null;
        }

        return (xAxis, yAxis);
    }

    private readonly record struct AxisLimits(double XMin, double XMax, double YMin, double YMax);
    private Window? _loadingDialog;

    private void ShowLoadingDialog(string message)
    {
        if (_loadingDialog is not null)
        {
            return;
        }

        _loadingDialog = new Window
        {
            Owner = this,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Width = 320,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White,
            AllowsTransparency = true,
            Content = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            FontSize = 16,
                            Margin = new Thickness(0,0,0,12)
                        },
                        new ProgressBar
                        {
                            IsIndeterminate = true,
                            Height = 18
                        }
                    }
                }
            }
        };

        _loadingDialog.Show();
    }

    private void HideLoadingDialog()
    {
        if (_loadingDialog is null)
        {
            return;
        }

        _loadingDialog.Close();
        _loadingDialog = null;
    }

    private sealed class DataSegmentComparer : IComparer
    {
        private readonly string _key;
        private readonly bool _ascending;

        public DataSegmentComparer(string key, bool ascending)
        {
            _key = key;
            _ascending = ascending;
        }

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return _ascending ? -1 : 1;
            }

            if (y is null)
            {
                return _ascending ? 1 : -1;
            }

            if (x is not DataSegment segmentX || y is not DataSegment segmentY)
            {
                return 0;
            }

            var valueX = GetComparableValue(segmentX, _key);
            var valueY = GetComparableValue(segmentY, _key);
            var comparer = Comparer<double?>.Default;
            var result = comparer.Compare(valueX, valueY);
            return _ascending ? result : -result;
        }

        private static double? GetComparableValue(DataSegment segment, string key) =>
            key switch
            {
                "Mean" => TryGetStatistic(segment, "Mean"),
                "StartRow" => segment.StartRow,
                "EndRow" => segment.EndRow,
                "Points" => segment.Rows.Count,
                "StdDev" => TryGetStatistic(segment, "StdDev"),
                _ => segment.StartRow
            };

        private static double? TryGetStatistic(DataSegment segment, string statKey)
        {
            if (segment.Statistics.TryGetValue(statKey, out var value))
            {
                return value;
            }

            return null;
        }
    }
}
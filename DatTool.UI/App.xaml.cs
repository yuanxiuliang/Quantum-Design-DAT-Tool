using System.Windows;
using DatTool.Services;
using DatTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DatTool.UI;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDatFileParser, DatFileParser>();
        services.AddSingleton<IMeasurementDefaultsProvider, MeasurementDefaultsProvider>();
        services.AddSingleton<ISegmentFilterService, SegmentFilterService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
        if (e.Args.Length > 0)
        {
            var initialFile = e.Args[0];
            if (!string.IsNullOrWhiteSpace(initialFile))
            {
                _ = mainWindow.LoadDatFileAsync(initialFile);
            }
        }
    }
}


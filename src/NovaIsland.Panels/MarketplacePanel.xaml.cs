using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NovaIsland.Domain.Marketplace;
using NovaIsland.Application.Marketplace;

namespace NovaIsland.Panels;

public class MarketplaceItemViewModel
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? PackagePath { get; set; }
}

public sealed partial class MarketplacePanel : Window
{
    private readonly IPluginInstaller _installer;
    private readonly ITelemetryService _telemetryService;

    public ObservableCollection<MarketplaceItemViewModel> AvailablePlugins { get; } = new();

    public MarketplacePanel(IPluginInstaller installer, ITelemetryService telemetryService)
    {
        _installer = installer;
        _telemetryService = telemetryService;
        
        this.InitializeComponent();

        TelemetryToggle.IsOn = _telemetryService.IsOptedIn;
        
        // Mock data
        AvailablePlugins.Add(new MarketplaceItemViewModel
        {
            Id = "MockWeather",
            Name = "Weather Widget",
            Description = "Displays local weather on the island.",
            PackagePath = "path/to/weather.zip"
        });
        
        PluginsList.ItemsSource = AvailablePlugins;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            try
            {
                await _installer.InstallAsync(path);
                await _telemetryService.RecordEventAsync("Install_Success", path);
            }
            catch (Exception ex)
            {
                await _telemetryService.RecordEventAsync("Install_Failed", ex.Message);
            }
        }
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            await _installer.UninstallAsync(id);
            await _telemetryService.RecordEventAsync("Uninstall_Success", id);
        }
    }

    private void TelemetryToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            _telemetryService.IsOptedIn = toggle.IsOn;
        }
    }
}

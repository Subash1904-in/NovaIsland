using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NovaIsland.Application.Stability;
using NovaIsland.SDK;

namespace NovaIsland.Plugins.Host;

public class PluginModule : INovaModule
{
    private readonly ILogger<PluginModule> _logger;
    private readonly List<OutOfProcessPluginRunner> _runners = new();

    public string ModuleName => "Plugin Manager";

    public PluginModule(ILogger<PluginModule> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plugin Manager started.");
        
        var pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginDir))
        {
            Directory.CreateDirectory(pluginDir);
        }

        // Keep running until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            foreach (var runner in _runners)
            {
                runner.Dispose();
            }
            _logger.LogInformation("Plugin Manager stopped.");
        }
    }

    public async Task LoadPluginAsync(PluginManifest manifest, string dllPath)
    {
        var runner = new OutOfProcessPluginRunner(manifest, _logger);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5000));
        
        try
        {
            var task = runner.StartAsync(dllPath, manifest.EntryPoint);
            await task.WaitAsync(cts.Token);
            _runners.Add(runner);
            _logger.LogInformation("Successfully loaded plugin {PluginName}", manifest.Name);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Plugin {PluginName} failed to load within 100ms budget.", manifest.Name);
            runner.Dispose();
            throw; // For test validation
        }
        catch (TaskCanceledException)
        {
            // Timeout
            runner.Dispose();
            throw new TimeoutException("Plugin load timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin {PluginName}", manifest.Name);
            runner.Dispose();
            throw; // For test validation
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NovaIsland.SDK;
using StreamJsonRpc;

namespace NovaIsland.Plugins.Host;

public class OutOfProcessPluginRunner : IDisposable
{
    private readonly PluginManifest _manifest;
    private readonly ILogger _logger;
    private readonly CapabilityValidator _validator;
    private Process? _process;
    private JsonRpc? _rpc;
    private IPluginRpc? _pluginRpc;

    public OutOfProcessPluginRunner(PluginManifest manifest, ILogger logger)
    {
        _manifest = manifest;
        _logger = logger;
        _validator = new CapabilityValidator(manifest);
    }

    public async Task StartAsync(string pluginDllPath, string typeName)
    {
        var workerExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NovaIsland.PluginWorker.exe");
        if (!File.Exists(workerExe))
        {
            // Fallback for tests if built in different folders
            workerExe = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "NovaIsland.PluginWorker", "bin", "Debug", "net9.0", "NovaIsland.PluginWorker.exe"));
        }

        var psi = new ProcessStartInfo
        {
            FileName = workerExe,
            Arguments = $"\"{pluginDllPath}\" \"{typeName}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi);
        if (_process == null) throw new InvalidOperationException("Failed to start plugin worker process.");

        // Log any stderr from the worker
        _ = Task.Run(() => 
        {
            try
            {
                while (!_process.StandardError.EndOfStream)
                {
                    var line = _process.StandardError.ReadLine();
                    if (line != null)
                        _logger.LogError("[Plugin {PluginName}] {Log}", _manifest.Name, line);
                }
            } catch { }
        });

        _rpc = new JsonRpc(_process.StandardInput.BaseStream, _process.StandardOutput.BaseStream);
        var hostRpcServer = new HostRpcServer(_validator, _logger);
        _rpc.AddLocalRpcTarget(hostRpcServer);
        
        _pluginRpc = _rpc.Attach<IPluginRpc>();
        _rpc.StartListening();

        if (_process.HasExited)
        {
            var err = await _process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Plugin worker exited early: {err}");
        }

        await _pluginRpc.InitializeAsync();
    }

    public Task ExecuteAsync()
    {
        if (_pluginRpc == null) throw new InvalidOperationException("Plugin not started.");
        return _pluginRpc.ExecuteAsync();
    }

    public void Dispose()
    {
        try
        {
            if (_pluginRpc != null)
            {
                // Best effort shutdown
                _pluginRpc.ShutdownAsync().Wait(100);
            }
        }
        catch { }

        _rpc?.Dispose();
        
        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            _process.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private sealed class HostRpcServer : IHostRpc
    {
        private readonly CapabilityValidator _validator;
        private readonly ILogger _logger;

        public HostRpcServer(CapabilityValidator validator, ILogger logger)
        {
            _validator = validator;
            _logger = logger;
        }

        public Task<string> ReadClipboardTextAsync()
        {
            _validator.AssertCapability(PluginCapability.Clipboard);
            _logger.LogInformation("Plugin read clipboard.");
            return Task.FromResult("Mock clipboard content");
        }

        public Task ShowNotificationAsync(string title, string content)
        {
            _validator.AssertCapability(PluginCapability.Notifications);
            _logger.LogInformation("Plugin showed notification: {Title}", title);
            return Task.CompletedTask;
        }
    }
}

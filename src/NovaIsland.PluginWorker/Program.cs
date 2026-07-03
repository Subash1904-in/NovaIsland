using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NovaIsland.SDK;
using StreamJsonRpc;

namespace NovaIsland.PluginWorker;

sealed class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            LogError("Usage: NovaIsland.PluginWorker <path-to-plugin-dll> <fully-qualified-type-name>");
            Environment.Exit(1);
        }

        var pluginPath = args[0];
        var typeName = args[1];
        if (!File.Exists(pluginPath))
        {
            LogError($"Plugin not found: {pluginPath}");
            Environment.Exit(1);
        }

        try
        {
            var assembly = Assembly.LoadFrom(pluginPath);
            var type = assembly.GetType(typeName);
            if (type == null || !typeof(IPlugin).IsAssignableFrom(type))
            {
                LogError($"Type {typeName} not found or does not implement IPlugin. Type is {type}.");
                Environment.Exit(1);
            }

            var pluginInstance = (IPlugin?)Activator.CreateInstance(type);

            if (pluginInstance == null)
            {
                LogError("Failed to instantiate plugin.");
                Environment.Exit(1);
            }

            using var stream = Console.OpenStandardInput();
            using var outStream = Console.OpenStandardOutput();
            
            var rpc = new JsonRpc(outStream, stream);
            var hostRpc = rpc.Attach<IHostRpc>();
            
            var rpcServer = new PluginRpcServer(pluginInstance, new RpcPluginContext(hostRpc));
            rpc.AddLocalRpcTarget(rpcServer);

            rpc.StartListening();
            
            await rpc.Completion;
        }
        catch (Exception ex)
        {
            File.AppendAllText("pluginworker_debug.log", $"Exception: {ex}\n");
            File.WriteAllText("pluginworker_error.log", ex.ToString());
            Console.Error.WriteLine($"Error starting plugin worker: {ex}");
            Environment.Exit(1);
        }
    }

    static void LogError(string msg)
    {
        File.WriteAllText("pluginworker_error.log", msg);
        Console.Error.WriteLine(msg);
    }
}

public sealed class PluginRpcServer : IPluginRpc
{
    private readonly IPlugin _plugin;
    private readonly IPluginContext _context;

    public PluginRpcServer(IPlugin plugin, IPluginContext context)
    {
        _plugin = plugin;
        _context = context;
    }

    public Task InitializeAsync() => _plugin.InitializeAsync(_context);
    public Task ExecuteAsync() => _plugin.ExecuteAsync();
    public Task ShutdownAsync() => _plugin.ShutdownAsync();
}

public sealed class RpcPluginContext : IPluginContext
{
    private readonly IHostRpc _hostRpc;

    public RpcPluginContext(IHostRpc hostRpc)
    {
        _hostRpc = hostRpc;
    }

    public Task ShowNotificationAsync(string title, string content) => _hostRpc.ShowNotificationAsync(title, content);
    public Task<string> ReadClipboardTextAsync() => _hostRpc.ReadClipboardTextAsync();
}

using System.Threading.Tasks;

namespace NovaIsland.SDK;

public interface IPluginContext
{
    // APIs available to the plugin (subject to Capability checks in host)
    Task ShowNotificationAsync(string title, string content);
    Task<string> ReadClipboardTextAsync();
}

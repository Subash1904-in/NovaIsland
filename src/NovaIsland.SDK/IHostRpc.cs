using System.Threading.Tasks;

namespace NovaIsland.SDK;

public interface IHostRpc
{
    Task ShowNotificationAsync(string title, string content);
    Task<string> ReadClipboardTextAsync();
}

using System.Threading;
using System.Threading.Tasks;

namespace NovaIsland.SDK;

public interface IPluginRpc
{
    Task InitializeAsync();
    Task ExecuteAsync();
    Task ShutdownAsync();
}

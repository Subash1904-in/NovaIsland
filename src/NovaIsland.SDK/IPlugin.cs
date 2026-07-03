using System.Threading;
using System.Threading.Tasks;

namespace NovaIsland.SDK;

public interface IPlugin
{
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);
    Task ExecuteAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

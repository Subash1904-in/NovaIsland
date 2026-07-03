using System.Threading.Tasks;

namespace NovaIsland.Domain.Marketplace;

public interface IPluginInstaller
{
    Task InstallAsync(string packageFilePath);
    Task UninstallAsync(string pluginId);
}

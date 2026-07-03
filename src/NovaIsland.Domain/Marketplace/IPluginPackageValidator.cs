using System.Threading.Tasks;

namespace NovaIsland.Domain.Marketplace;

public interface IPluginPackageValidator
{
    /// <summary>
    /// Validates the plugin package structure and signature.
    /// Throws System.Security.SecurityException if tampered or invalid.
    /// </summary>
    Task ValidatePackageAsync(string extractedDirectoryPath);
}

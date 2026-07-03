using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Threading.Tasks;
using NovaIsland.Domain.Marketplace;

namespace NovaIsland.Application.Marketplace;

public class PluginInstaller : IPluginInstaller
{
    private readonly IPluginPackageValidator _validator;
    private readonly string _pluginsDirectory;
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PluginInstaller(IPluginPackageValidator validator, string? pluginsDirectory = null)
    {
        _validator = validator;
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland", "Plugins");
    }

    public async Task InstallAsync(string packageFilePath)
    {
        if (!File.Exists(packageFilePath))
            throw new FileNotFoundException("Plugin package not found", packageFilePath);

        Directory.CreateDirectory(_pluginsDirectory);

        string tempDir = Path.Combine(_pluginsDirectory, $".tmp_{Guid.NewGuid()}");
        
        try
        {
            // Extract
            ZipFile.ExtractToDirectory(packageFilePath, tempDir);

            // Verify
            await _validator.ValidatePackageAsync(tempDir);

            // Determine final path based on manifest
            string manifestPath = Path.Combine(tempDir, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new SecurityException("Missing manifest.json");

            string manifestJson = File.ReadAllText(manifestPath);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<NovaIsland.SDK.PluginManifest>(manifestJson, _jsonOptions);
            
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
                throw new SecurityException("Invalid manifest.json");

            string finalDir = Path.Combine(_pluginsDirectory, manifest.Id);

            // Atomic commit
            if (Directory.Exists(finalDir))
            {
                Directory.Delete(finalDir, true);
            }
            Directory.Move(tempDir, finalDir);
        }
        catch
        {
            // Rollback
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            throw;
        }
    }

    public Task UninstallAsync(string pluginId)
    {
        string finalDir = Path.Combine(_pluginsDirectory, pluginId);
        if (Directory.Exists(finalDir))
        {
            Directory.Delete(finalDir, true);
        }
        return Task.CompletedTask;
    }
}

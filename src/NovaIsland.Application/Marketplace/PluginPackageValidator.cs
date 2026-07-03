using System;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using NovaIsland.Domain.Marketplace;

namespace NovaIsland.Application.Marketplace;

public class PluginPackageValidator : IPluginPackageValidator
{
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public Task ValidatePackageAsync(string extractedDirectoryPath)
    {
        string manifestPath = Path.Combine(extractedDirectoryPath, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new SecurityException("Package must contain a manifest.json");

        string manifestJson = File.ReadAllText(manifestPath);
        var manifest = System.Text.Json.JsonSerializer.Deserialize<NovaIsland.SDK.PluginManifest>(manifestJson, _jsonOptions);

        if (manifest == null)
            throw new SecurityException("Invalid manifest.json");

        if (string.IsNullOrWhiteSpace(manifest.Signature))
            throw new SecurityException("Package is unsigned and cannot be verified.");

        // Simulate signature verification:
        // In a real implementation, we would hash the package contents and verify against a public key.
        // For simulation, if Signature == "tampered", we fail.
        if (manifest.Signature == "tampered")
            throw new SecurityException("Package signature is invalid or tampered.");

        return Task.CompletedTask;
    }
}

using System;
using System.Security;
using NovaIsland.SDK;

namespace NovaIsland.Plugins.Host;

public class CapabilityValidator
{
    private readonly PluginManifest _manifest;

    public CapabilityValidator(PluginManifest manifest)
    {
        _manifest = manifest;
    }

    public void AssertCapability(PluginCapability required)
    {
        if ((_manifest.Capabilities & required) != required)
        {
            throw new SecurityException($"Plugin '{_manifest.Name}' attempted to use {required} without declaring it in its manifest.");
        }
    }
}

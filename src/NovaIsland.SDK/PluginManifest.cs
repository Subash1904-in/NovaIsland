using System;

namespace NovaIsland.SDK;

public record PluginManifest(
    string Id,
    string Name,
    string Version,
    string Author,
    string EntryPoint,
    PluginCapability Capabilities,
    string Signature = ""
);

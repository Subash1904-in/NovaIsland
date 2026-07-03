using System;

namespace NovaIsland.SDK;

[Flags]
public enum PluginCapability
{
    None = 0,
    Network = 1,
    FileSystem = 2,
    Clipboard = 4,
    Notifications = 8,
    All = Network | FileSystem | Clipboard | Notifications
}

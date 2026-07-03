namespace NovaIsland.Domain.Widgets;

[Flags]
public enum WidgetCapabilities
{
    None = 0,
    Network = 1,
    FileSystem = 2,
    Location = 4,
    Clipboard = 8
}

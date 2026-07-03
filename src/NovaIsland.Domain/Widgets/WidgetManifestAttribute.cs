namespace NovaIsland.Domain.Widgets;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WidgetManifestAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public WidgetCapabilities RequiredCapabilities { get; }

    public WidgetManifestAttribute(string id, string name, WidgetCapabilities requiredCapabilities = WidgetCapabilities.None)
    {
        Id = id;
        Name = name;
        RequiredCapabilities = requiredCapabilities;
    }
}

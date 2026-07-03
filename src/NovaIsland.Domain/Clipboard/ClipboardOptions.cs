namespace NovaIsland.Domain.Clipboard;

public class ClipboardOptions
{
    public const string SectionName = "Clipboard";

    /// <summary>
    /// How long to retain unpinned clipboard entries before eviction.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Regex patterns to detect sensitive entries (e.g. passwords, secrets)
    /// to store them with extra care or skip displaying them on the UI.
    /// </summary>
    public List<string> SensitivePatterns { get; set; } = new();
}

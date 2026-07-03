namespace NovaIsland.Domain.Clipboard;

public enum ClipboardEntryType
{
    Text = 0,
    Image = 1,
    File = 2
}

/// <summary>
/// Represents an entry in the clipboard history.
/// </summary>
public class ClipboardEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ClipboardEntryType Type { get; set; }
    
    /// <summary>
    /// Content string (e.g., text content, or file paths separated by newline).
    /// </summary>
    public string? Content { get; set; }
    
    /// <summary>
    /// Binary blob for images or other rich data.
    /// </summary>
    public byte[]? Blob { get; set; }
    
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool IsPinned { get; set; }
    
    /// <summary>
    /// Indicates if the entry was matched against a sensitive regex pattern (e.g. password)
    /// </summary>
    public bool IsSensitive { get; set; }
}

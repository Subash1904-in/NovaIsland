namespace NovaIsland.UI.Shell;

/// <summary>
/// Represents the user interaction state of the island, which dictates how much content is shown.
/// </summary>
public enum IslandInteractionState
{
    /// <summary>
    /// The default compact state.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Slightly expanded state on hover to show the subtitle.
    /// </summary>
    Peek = 1,

    /// <summary>
    /// Fully expanded state on click to show the detailed module rows.
    /// </summary>
    FullExpanded = 2
}

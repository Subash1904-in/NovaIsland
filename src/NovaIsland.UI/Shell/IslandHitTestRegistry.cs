using System;
using System.Drawing;

namespace NovaIsland.UI.Shell;

/// <summary>
/// Maintains a list of hit-testable regions and their associated click actions.
/// Designed for zero-allocation on the hot path (hit testing).
/// </summary>
public sealed class IslandHitTestRegistry
{
    private struct HitRect
    {
        public RectangleF Bounds;
        public Action OnClick;
    }

    private HitRect[] _rects = new HitRect[16]; // Pooled array
    private int _count;

    /// <summary>
    /// Clears all registered hit-test regions.
    /// </summary>
    public void Clear()
    {
        // Null out actions to allow GC, but keep array.
        for (int i = 0; i < _count; i++)
        {
            _rects[i].OnClick = null!;
        }
        _count = 0;
    }

    /// <summary>
    /// Registers a new hit-test region.
    /// </summary>
    /// <param name="bounds">The bounds in logical pixels relative to the island window.</param>
    /// <param name="onClick">The action to invoke when clicked.</param>
    public void Register(RectangleF bounds, Action onClick)
    {
        if (_count >= _rects.Length)
        {
            Array.Resize(ref _rects, _rects.Length * 2);
        }

        _rects[_count] = new HitRect
        {
            Bounds = bounds,
            OnClick = onClick
        };
        _count++;
    }

    /// <summary>
    /// Hit-tests a point against all registered regions.
    /// If a match is found, invokes the associated action and returns true.
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <returns>True if a registered region was hit and its action invoked; otherwise, false.</returns>
    public bool HitTest(float x, float y)
    {
        for (int i = 0; i < _count; i++)
        {
            ref var rect = ref _rects[i];
            if (rect.Bounds.Contains(x, y))
            {
                rect.OnClick?.Invoke();
                return true;
            }
        }
        return false;
    }
}

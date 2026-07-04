using System;
using System.Diagnostics;
using NovaIsland.UI.Shell;

namespace NovaIsland.UI.Animation;

/// <summary>
/// Accessibility-focused animator that replaces spring physics with instant transitions
/// and a short opacity cross-fade. Used when the user enables reduced-motion mode.
/// </summary>
/// <remarks>
/// <para>
/// When reduced motion is enabled, all dimensional properties (width, height, corner radius,
/// offset) snap instantly to target values. Only opacity uses a short linear fade
/// (default 150ms) to avoid jarring visual pops.
/// </para>
/// <para>
/// <b>Zero-allocation</b>: Same hot-path constraints as <see cref="IslandAnimationController"/>.
/// No LINQ, no boxing, no closures, no per-frame allocations.
/// </para>
/// </remarks>
public sealed class ReducedMotionAnimator : IIslandAnimator
{
    /// <summary>Duration of the opacity cross-fade in seconds.</summary>
    private const float CrossFadeDurationSeconds = 0.15f;

    private float _currentWidth;
    private float _currentHeight;
    private float _currentCornerRadius;
    private float _currentOpacity;
    private float _currentOffsetY;

    private float _targetOpacity;
    private float _startOpacity;
    private float _fadeElapsed;
    private bool _isFading;

    private IslandState _currentTarget;
    private IslandInteractionState _currentInteractionTarget;

    /// <summary>
    /// Initializes a new <see cref="ReducedMotionAnimator"/> at the specified initial state.
    /// </summary>
    /// <param name="initialState">The starting state of the island.</param>
    public ReducedMotionAnimator(IslandState initialState)
    {
        _currentTarget = initialState;
        _currentInteractionTarget = IslandInteractionState.Idle;

        ref readonly var desc = ref IslandStateDescriptors.GetDescriptor(initialState);
        _currentWidth = desc.Width;
        _currentHeight = desc.Height;
        _currentCornerRadius = desc.CornerRadius;
        _currentOpacity = desc.Opacity;
        _currentOffsetY = desc.OffsetY;

        _targetOpacity = desc.Opacity;
        _isFading = false;
    }

    /// <inheritdoc />
    public IslandState CurrentTarget => _currentTarget;

    /// <inheritdoc />
    public IslandInteractionState CurrentInteractionTarget => _currentInteractionTarget;

    /// <inheritdoc />
    public bool IsSettled => !_isFading;

    /// <inheritdoc />
    /// <remarks>
    /// Instantly snaps width, height, corner radius, and offset to target values.
    /// Only opacity uses a short cross-fade for visual smoothness.
    /// </remarks>
    public void TransitionTo(IslandState target, IslandInteractionState interactionTarget = IslandInteractionState.Idle, SpringConfig? overrideConfig = null)
    {
        _currentTarget = target;
        _currentInteractionTarget = interactionTarget;

        ref readonly var desc = ref IslandStateDescriptors.GetDescriptor(target);

        float targetWidth = desc.Width;
        float targetHeight = desc.Height;
        float targetCornerRadius = desc.CornerRadius;
        
        if (interactionTarget == IslandInteractionState.Peek)
        {
            targetWidth = 300f;
            targetHeight = 60f;
            targetCornerRadius = 14f;
        }
        else if (interactionTarget == IslandInteractionState.FullExpanded)
        {
            targetWidth = 400f;
            targetHeight = 320f;
            targetCornerRadius = 16f;
        }

        // Snap dimensional properties instantly.
        _currentWidth = targetWidth;
        _currentHeight = targetHeight;
        _currentCornerRadius = targetCornerRadius;
        _currentOffsetY = desc.OffsetY;

        // Start opacity cross-fade if opacity changes.
        if (MathF.Abs(_currentOpacity - desc.Opacity) > 0.001f)
        {
            _startOpacity = _currentOpacity;
            _targetOpacity = desc.Opacity;
            _fadeElapsed = 0f;
            _isFading = true;
        }
        else
        {
            _currentOpacity = desc.Opacity;
            _targetOpacity = desc.Opacity;
            _isFading = false;
        }
    }

    /// <inheritdoc />
    public void Update(float deltaTime)
    {
        if (!_isFading)
        {
            return;
        }

        _fadeElapsed += deltaTime;
        float t = MathF.Min(_fadeElapsed / CrossFadeDurationSeconds, 1f);

        // Linear interpolation for the cross-fade.
        _currentOpacity = _startOpacity + ((_targetOpacity - _startOpacity) * t);

        if (t >= 1f)
        {
            _currentOpacity = _targetOpacity;
            _isFading = false;
        }
    }

    /// <inheritdoc />
    public void GetCurrentValues(out float width, out float height, out float cornerRadius, out float opacity, out float offsetY)
    {
        width = _currentWidth;
        height = _currentHeight;
        cornerRadius = _currentCornerRadius;
        opacity = _currentOpacity;
        offsetY = _currentOffsetY;
    }
}

namespace NovaIsland.UI.Animation;

/// <summary>
/// Defines the contract for island animation controllers.
/// Two implementations exist: <see cref="IslandAnimationController"/> (spring physics)
/// and <see cref="ReducedMotionAnimator"/> (instant/cross-fade for accessibility).
/// </summary>
/// <remarks>
/// All methods on this interface are called from the frame-pacing hot path
/// and must be zero-allocation. No LINQ, no boxing, no closures, no string operations.
/// </remarks>
public interface IIslandAnimator
{
    /// <summary>
    /// Gets the current target state.
    /// </summary>
    IslandState CurrentTarget { get; }

    /// <summary>
    /// Gets a value indicating whether the animation has settled (converged to target).
    /// When true, the frame-pacing service may pause updates to save CPU.
    /// </summary>
    bool IsSettled { get; }

    /// <summary>
    /// Transitions to a new target state. If an animation is already in-flight,
    /// the spring re-targets without resetting velocity (interruptible transition).
    /// </summary>
    /// <param name="target">The new target state.</param>
    void TransitionTo(IslandState target);

    /// <summary>
    /// Advances the animation by the given delta time. Called once per frame.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since the last frame, in seconds.</param>
    void Update(float deltaTime);

    /// <summary>
    /// Reads the current animated values. All parameters are out to avoid struct allocation.
    /// </summary>
    /// <param name="width">Current interpolated width.</param>
    /// <param name="height">Current interpolated height.</param>
    /// <param name="cornerRadius">Current interpolated corner radius.</param>
    /// <param name="opacity">Current interpolated opacity.</param>
    /// <param name="offsetY">Current interpolated vertical offset.</param>
    void GetCurrentValues(out float width, out float height, out float cornerRadius, out float opacity, out float offsetY);
}

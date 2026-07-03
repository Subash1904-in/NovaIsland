namespace NovaIsland.UI.Animation;

/// <summary>
/// Manages multi-dimensional spring animation for island state transitions.
/// Controls 5 independent springs (Width, Height, CornerRadius, Opacity, OffsetY)
/// that animate simultaneously when transitioning between <see cref="IslandState"/> values.
/// </summary>
/// <remarks>
/// <para>
/// <b>Interruptibility</b>: When <see cref="TransitionTo"/> is called during an in-flight
/// animation, the springs are re-targeted to the new state values WITHOUT resetting velocity.
/// This produces smooth, physically-plausible redirections instead of jarring restarts.
/// </para>
/// <para>
/// <b>Zero-allocation</b>: All fields are value types (structs). No arrays, no LINQ,
/// no boxing, no closures. The <see cref="Update"/> method is safe for per-frame hot-path use.
/// </para>
/// </remarks>
public sealed class IslandAnimationController : IIslandAnimator
{
    // 5 independent springs — stored as fields, not array, to avoid bounds checks.
    private SpringState _widthSpring;
    private SpringState _heightSpring;
    private SpringState _cornerRadiusSpring;
    private SpringState _opacitySpring;
    private SpringState _offsetYSpring;

    // Target values for each dimension.
    private float _targetWidth;
    private float _targetHeight;
    private float _targetCornerRadius;
    private float _targetOpacity;
    private float _targetOffsetY;

    private SpringConfig _config;
    private IslandState _currentTarget;

    /// <summary>
    /// Initializes a new <see cref="IslandAnimationController"/> at the specified initial state.
    /// </summary>
    /// <param name="initialState">The starting state of the island.</param>
    /// <param name="config">Spring configuration (stiffness and damping).</param>
    public IslandAnimationController(IslandState initialState, SpringConfig config)
    {
        _config = config;
        _currentTarget = initialState;

        ref readonly var desc = ref IslandStateDescriptors.GetDescriptor(initialState);

        // Initialize all springs at the target position (already settled).
        _widthSpring = new SpringState(desc.Width);
        _heightSpring = new SpringState(desc.Height);
        _cornerRadiusSpring = new SpringState(desc.CornerRadius);
        _opacitySpring = new SpringState(desc.Opacity);
        _offsetYSpring = new SpringState(desc.OffsetY);

        _targetWidth = desc.Width;
        _targetHeight = desc.Height;
        _targetCornerRadius = desc.CornerRadius;
        _targetOpacity = desc.Opacity;
        _targetOffsetY = desc.OffsetY;
    }

    /// <inheritdoc />
    public IslandState CurrentTarget => _currentTarget;

    /// <inheritdoc />
    public bool IsSettled =>
        SpringAnimator.IsSettled(in _widthSpring, _targetWidth) &&
        SpringAnimator.IsSettled(in _heightSpring, _targetHeight) &&
        SpringAnimator.IsSettled(in _cornerRadiusSpring, _targetCornerRadius) &&
        SpringAnimator.IsSettled(in _opacitySpring, _targetOpacity) &&
        SpringAnimator.IsSettled(in _offsetYSpring, _targetOffsetY);

    /// <summary>
    /// Gets or sets the spring configuration. Allows runtime tuning.
    /// </summary>
    public SpringConfig Config
    {
        get => _config;
        set => _config = value;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Re-targets all springs to the new state's descriptor values.
    /// Velocity is preserved, making the transition interruptible — a new target
    /// smoothly redirects the in-flight animation rather than restarting it.
    /// </remarks>
    public void TransitionTo(IslandState target)
    {
        _currentTarget = target;

        ref readonly var desc = ref IslandStateDescriptors.GetDescriptor(target);

        // Re-target without resetting velocity (interruptible).
        _targetWidth = desc.Width;
        _targetHeight = desc.Height;
        _targetCornerRadius = desc.CornerRadius;
        _targetOpacity = desc.Opacity;
        _targetOffsetY = desc.OffsetY;
    }

    /// <inheritdoc />
    /// <remarks>
    /// ZERO-ALLOC: This method calls <see cref="SpringAnimator.Advance"/> on each
    /// spring dimension using refs. No heap allocations, no boxing, no LINQ.
    /// </remarks>
    public void Update(float deltaTime)
    {
        SpringAnimator.Advance(ref _widthSpring, _targetWidth, in _config, deltaTime);
        SpringAnimator.Advance(ref _heightSpring, _targetHeight, in _config, deltaTime);
        SpringAnimator.Advance(ref _cornerRadiusSpring, _targetCornerRadius, in _config, deltaTime);
        SpringAnimator.Advance(ref _opacitySpring, _targetOpacity, in _config, deltaTime);
        SpringAnimator.Advance(ref _offsetYSpring, _targetOffsetY, in _config, deltaTime);
    }

    /// <inheritdoc />
    public void GetCurrentValues(out float width, out float height, out float cornerRadius, out float opacity, out float offsetY)
    {
        width = _widthSpring.Position;
        height = _heightSpring.Position;
        cornerRadius = _cornerRadiusSpring.Position;
        opacity = _opacitySpring.Position;
        offsetY = _offsetYSpring.Position;
    }
}

namespace NovaIsland.UI.Animation;

/// <summary>
/// Pure-math critically-damped spring simulation.
/// All operations are zero-allocation and operate on value-type refs.
/// This is the inner-loop core of the island animation system.
/// </summary>
/// <remarks>
/// Uses the closed-form solution for a critically-damped spring:
///   x(t) = target + (A + B*t) * e^(-ω*t)
/// where ω = sqrt(stiffness), A = x0 - target, B = v0 + ω*A.
///
/// For near-critically-damped springs (damping ratio ≈ 1.0), we use the
/// semi-implicit Euler integration which is simpler, stable, and allows
/// easy re-targeting without recomputing closed-form coefficients.
/// </remarks>
public static class SpringAnimator
{
    /// <summary>
    /// Convergence threshold. When |position - target| and |velocity| are both
    /// below this value, the spring is considered settled.
    /// </summary>
    private const float SettleThreshold = 0.01f;

    /// <summary>
    /// Maximum delta time to prevent instability from large time steps.
    /// Clamps to 1/15th of a second (prevents spiral-of-death on lag spikes).
    /// </summary>
    private const float MaxDeltaTime = 1f / 15f;

    /// <summary>
    /// Advances a single spring dimension by one time step using semi-implicit Euler integration.
    /// </summary>
    /// <param name="state">The current spring state (position and velocity). Modified in-place.</param>
    /// <param name="target">The target position the spring is moving toward.</param>
    /// <param name="config">The spring configuration (stiffness and damping).</param>
    /// <param name="deltaTime">Time elapsed since the last update, in seconds.</param>
    /// <remarks>
    /// ZERO-ALLOC: This method operates entirely on stack values via refs.
    /// No heap allocation, no boxing, no LINQ. Safe for per-frame hot path.
    ///
    /// Semi-implicit Euler:
    ///   acceleration = -stiffness * (position - target) - damping * velocity
    ///   velocity += acceleration * dt
    ///   position += velocity * dt
    ///
    /// This is unconditionally stable for critically/over-damped springs with reasonable dt.
    /// </remarks>
    public static void Advance(ref SpringState state, float target, in SpringConfig config, float deltaTime)
    {
        // Clamp delta time to prevent instability.
        float dt = MathF.Min(deltaTime, MaxDeltaTime);

        // Sub-step for stability at low frame rates.
        // At 60 Hz, dt ≈ 0.0167s → 1 step. At 30 Hz, dt ≈ 0.033s → 2 steps.
        const float subStepSize = 1f / 120f;
        int steps = (int)MathF.Ceiling(dt / subStepSize);
        float stepDt = dt / steps;

        for (int i = 0; i < steps; i++)
        {
            float displacement = state.Position - target;
            float acceleration = (-config.Stiffness * displacement) - (config.Damping * state.Velocity);
            state.Velocity += acceleration * stepDt;
            state.Position += state.Velocity * stepDt;
        }

        // Snap to target when settled to avoid perpetual micro-oscillation.
        if (MathF.Abs(state.Position - target) < SettleThreshold &&
            MathF.Abs(state.Velocity) < SettleThreshold)
        {
            state.Position = target;
            state.Velocity = 0f;
        }
    }

    /// <summary>
    /// Checks whether a spring has settled at its target position.
    /// </summary>
    /// <param name="state">The spring state to check.</param>
    /// <param name="target">The target position.</param>
    /// <returns>True if the spring has converged.</returns>
    public static bool IsSettled(in SpringState state, float target)
    {
        return MathF.Abs(state.Position - target) < SettleThreshold &&
               MathF.Abs(state.Velocity) < SettleThreshold;
    }
}

/// <summary>
/// Mutable state of a single spring dimension.
/// Value type to avoid heap allocation. Passed by ref in the hot path.
/// </summary>
public struct SpringState
{
    /// <summary>Current position of the spring.</summary>
    public float Position;

    /// <summary>Current velocity of the spring.</summary>
    public float Velocity;

    /// <summary>
    /// Initializes a spring at the given position with zero velocity.
    /// </summary>
    /// <param name="position">Initial position.</param>
    public SpringState(float position)
    {
        Position = position;
        Velocity = 0f;
    }
}

/// <summary>
/// Configuration for a spring simulation. Readonly struct for zero-alloc pass-by-value.
/// </summary>
public readonly struct SpringConfig
{
    /// <summary>Spring stiffness (force per unit displacement). Higher = faster response.</summary>
    public readonly float Stiffness;

    /// <summary>Damping coefficient (force per unit velocity). Controls oscillation.</summary>
    public readonly float Damping;

    /// <summary>
    /// Initializes a spring with explicit stiffness and damping.
    /// </summary>
    /// <param name="stiffness">Spring stiffness.</param>
    /// <param name="damping">Damping coefficient.</param>
    public SpringConfig(float stiffness, float damping)
    {
        Stiffness = stiffness;
        Damping = damping;
    }

    /// <summary>
    /// Creates a critically-damped spring configuration.
    /// Critical damping produces the fastest convergence without oscillation.
    /// Damping = 2 * sqrt(stiffness) gives a damping ratio of exactly 1.0.
    /// </summary>
    /// <param name="stiffness">Spring stiffness.</param>
    /// <returns>A critically-damped <see cref="SpringConfig"/>.</returns>
    public static SpringConfig CriticallyDamped(float stiffness)
    {
        return new SpringConfig(stiffness, 2f * MathF.Sqrt(stiffness));
    }
}

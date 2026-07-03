using FluentAssertions;
using NovaIsland.UI.Animation;
using Xunit;

namespace NovaIsland.Tests.Unit.Animation;

/// <summary>
/// Tests for <see cref="SpringAnimator"/> — the critically-damped spring physics engine.
/// Validates convergence, stability, and zero-oscillation behavior.
/// </summary>
public sealed class SpringAnimatorTests
{
    [Fact]
    public void CriticallyDamped_Factory_Produces_CorrectDampingRatio()
    {
        // Arrange
        const float stiffness = 300f;

        // Act
        var config = SpringConfig.CriticallyDamped(stiffness);

        // Assert — damping = 2 * sqrt(stiffness)
        float expectedDamping = 2f * MathF.Sqrt(stiffness);
        config.Stiffness.Should().Be(stiffness);
        config.Damping.Should().BeApproximately(expectedDamping, 0.001f);
    }

    [Fact]
    public void Spring_Converges_To_Target_WithinReasonableTime()
    {
        // Arrange
        var config = SpringConfig.CriticallyDamped(300f);
        var state = new SpringState(0f);
        const float target = 100f;

        // Act — simulate 2 seconds at 120 Hz
        for (int i = 0; i < 240; i++)
        {
            SpringAnimator.Advance(ref state, target, in config, 1f / 120f);
        }

        // Assert
        state.Position.Should().BeApproximately(target, 0.01f,
            "spring should converge to target within 2 seconds");
        SpringAnimator.IsSettled(in state, target).Should().BeTrue();
    }

    [Fact]
    public void Spring_DoesNotOscillate_WithCriticalDamping()
    {
        // Arrange
        var config = SpringConfig.CriticallyDamped(300f);
        var state = new SpringState(0f);
        const float target = 100f;

        // Act — track maximum overshoot
        float maxPosition = 0f;
        for (int i = 0; i < 240; i++)
        {
            SpringAnimator.Advance(ref state, target, in config, 1f / 120f);
            if (state.Position > maxPosition)
            {
                maxPosition = state.Position;
            }
        }

        // Assert — should not overshoot target (critically damped = no oscillation)
        maxPosition.Should().BeLessOrEqualTo(target + 0.5f,
            "critically-damped spring should not significantly overshoot");
    }

    [Fact]
    public void Spring_Retarget_Preserves_Velocity()
    {
        // Arrange
        var config = SpringConfig.CriticallyDamped(300f);
        var state = new SpringState(0f);

        // Act — advance toward 100 for a few frames
        for (int i = 0; i < 10; i++)
        {
            SpringAnimator.Advance(ref state, 100f, in config, 1f / 120f);
        }

        float velocityBeforeRetarget = state.Velocity;
        velocityBeforeRetarget.Should().NotBe(0f, "spring should have velocity mid-animation");

        // Retarget to 50 — velocity is not reset (just change target)
        SpringAnimator.Advance(ref state, 50f, in config, 1f / 120f);

        // Assert — velocity should have changed smoothly (not reset to 0)
        // The velocity direction may change, but it shouldn't snap to zero
        state.Velocity.Should().NotBe(0f,
            "velocity should not snap to zero on retarget");
    }

    [Fact]
    public void Spring_Handles_LargeDeltaTime_Gracefully()
    {
        // Arrange
        var config = SpringConfig.CriticallyDamped(300f);
        var state = new SpringState(0f);
        const float target = 100f;

        // Act — simulate a huge lag spike (1 second delta)
        SpringAnimator.Advance(ref state, target, in config, 1.0f);

        // Assert — should not produce NaN or infinity
        float.IsNaN(state.Position).Should().BeFalse();
        float.IsInfinity(state.Position).Should().BeFalse();
        float.IsNaN(state.Velocity).Should().BeFalse();
        float.IsInfinity(state.Velocity).Should().BeFalse();
    }

    [Fact]
    public void Spring_AlreadyAtTarget_Stays_Settled()
    {
        // Arrange
        var config = SpringConfig.CriticallyDamped(300f);
        var state = new SpringState(100f);
        const float target = 100f;

        // Act
        SpringAnimator.Advance(ref state, target, in config, 1f / 120f);

        // Assert
        state.Position.Should().Be(target);
        state.Velocity.Should().Be(0f);
        SpringAnimator.IsSettled(in state, target).Should().BeTrue();
    }

    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(165)]
    public void Spring_Converges_At_AllRefreshRates(int refreshRate)
    {
        // Arrange
        var config = SpringConfig.CriticallyDamped(300f);
        var state = new SpringState(0f);
        const float target = 200f;
        float dt = 1f / refreshRate;
        int frames = refreshRate * 3; // 3 seconds

        // Act
        for (int i = 0; i < frames; i++)
        {
            SpringAnimator.Advance(ref state, target, in config, dt);
        }

        // Assert
        state.Position.Should().BeApproximately(target, 0.1f,
            $"spring should converge at {refreshRate} Hz within 3 seconds");
    }
}
